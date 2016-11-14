﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NPoco;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.DI;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Editors;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.Rdbms;

using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Factories;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Core.Persistence.Mappers;

namespace Umbraco.Core.Persistence.Repositories
{
    // this cannot be inside VersionableRepositoryBase because that class is static
    internal static class VersionableRepositoryBaseAliasRegex
    {
        private static readonly Dictionary<Type, Regex> Regexes = new Dictionary<Type, Regex>();

        public static Regex For(ISqlSyntaxProvider sqlSyntax)
        {
            var type = sqlSyntax.GetType();
            Regex aliasRegex;
            if (Regexes.TryGetValue(type, out aliasRegex))
                return aliasRegex;

            var col = Regex.Escape(sqlSyntax.GetQuotedColumnName("column")).Replace("column", @"\w+");
            var fld = Regex.Escape(sqlSyntax.GetQuotedTableName("table") + ".").Replace("table", @"\w+") + col;
            aliasRegex = new Regex("(" + fld + @")\s+AS\s+(" + col + ")", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Regexes[type] = aliasRegex;
            return aliasRegex;
        }
    }

    internal abstract class VersionableRepositoryBase<TId, TEntity, TRepository> : NPocoRepositoryBase<TId, TEntity>
        where TEntity : class, IAggregateRoot
        where TRepository : class, IRepository
    {
        //private readonly IContentSection _contentSection;

        protected VersionableRepositoryBase(IDatabaseUnitOfWork work, CacheHelper cache, ILogger logger, IContentSection contentSection, IMapperCollection mappers)
            : base(work, cache, logger, mappers)
        {
            //_contentSection = contentSection;
        }

        protected abstract TRepository Instance { get; }

        #region IRepositoryVersionable Implementation

        /// <summary>
        /// Gets a list of all versions for an <see cref="TEntity"/> ordered so latest is first
        /// </summary>
        /// <param name="id">Id of the <see cref="TEntity"/> to retrieve versions from</param>
        /// <returns>An enumerable list of the same <see cref="TEntity"/> object with different versions</returns>
        public virtual IEnumerable<TEntity> GetAllVersions(int id)
        {
            var sql = Sql()
                .SelectAll()
                .From<ContentVersionDto>()
                .InnerJoin<ContentDto>()
                .On<ContentVersionDto, ContentDto>(left => left.NodeId, right => right.NodeId)
                .InnerJoin<NodeDto>()
                .On<ContentDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId)
                .Where<NodeDto>(x => x.NodeId == id)
                .OrderByDescending<ContentVersionDto>(x => x.VersionDate);

            var dtos = Database.Fetch<ContentVersionDto>(sql);
            return dtos.Select(x => GetByVersion(x.VersionId));
        }

        /// <summary>
        /// Gets a list of all version Ids for the given content item ordered so latest is first
        /// </summary>
        /// <param name="id"></param>
        /// <param name="maxRows">The maximum number of rows to return</param>
        /// <returns></returns>
        public virtual IEnumerable<Guid> GetVersionIds(int id, int maxRows)
        {
            var sql = Sql();
            sql.Select("cmsDocument.versionId")
                .From<DocumentDto>()
                .InnerJoin<ContentDto>()
                .On<DocumentDto, ContentDto>(left => left.NodeId, right => right.NodeId)
                .InnerJoin<NodeDto>()
                .On<ContentDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId)
                .Where<NodeDto>(x => x.NodeId == id)
                .OrderByDescending<DocumentDto>(x => x.UpdateDate);

            return Database.Fetch<Guid>(SqlSyntax.SelectTop(sql, maxRows));
        }

        public virtual void DeleteVersion(Guid versionId)
        {
            var dto = Database.FirstOrDefault<ContentVersionDto>("WHERE versionId = @VersionId", new { VersionId = versionId });
            if(dto == null) return;

            //Ensure that the lastest version is not deleted
            var latestVersionDto = Database.FirstOrDefault<ContentVersionDto>("WHERE ContentId = @Id ORDER BY VersionDate DESC", new { Id = dto.NodeId });
            if(latestVersionDto.VersionId == dto.VersionId)
                return;

            PerformDeleteVersion(dto.NodeId, versionId);
        }

        public virtual void DeleteVersions(int id, DateTime versionDate)
        {
            //Ensure that the latest version is not part of the versions being deleted
            var latestVersionDto = Database.FirstOrDefault<ContentVersionDto>("WHERE ContentId = @Id ORDER BY VersionDate DESC", new { Id = id });
            var list =
                Database.Fetch<ContentVersionDto>(
                    "WHERE versionId <> @VersionId AND (ContentId = @Id AND VersionDate < @VersionDate)",
                    new { /*VersionId =*/ latestVersionDto.VersionId, Id = id, VersionDate = versionDate});
            if (list.Any() == false) return;

            foreach (var dto in list)
            {
                PerformDeleteVersion(id, dto.VersionId);
            }
        }

        public abstract TEntity GetByVersion(Guid versionId);

        /// <summary>
        /// Protected method to execute the delete statements for removing a single version for a TEntity item.
        /// </summary>
        /// <param name="id">Id of the <see cref="TEntity"/> to delete a version from</param>
        /// <param name="versionId">Guid id of the version to delete</param>
        protected abstract void PerformDeleteVersion(int id, Guid versionId);

        #endregion

        public int CountDescendants(int parentId, string contentTypeAlias = null)
        {
            var pathMatch = parentId == -1
                ? "-1,"
                : "," + parentId + ",";

            var sql = Sql()
                .SelectCount()
                .From<NodeDto>();

            if (contentTypeAlias.IsNullOrWhiteSpace())
            {
                sql
                    .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId)
                    .Where<NodeDto>(x => x.Path.Contains(pathMatch));
            }
            else
            {
                sql
                    .InnerJoin<ContentDto>()
                    .On<NodeDto, ContentDto>(left => left.NodeId, right => right.NodeId)
                    .InnerJoin<ContentTypeDto>()
                    .On<ContentTypeDto, ContentDto>(left => left.NodeId, right => right.ContentTypeId)
                    .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId)
                    .Where<NodeDto>(x => x.Path.Contains(pathMatch))
                    .Where<ContentTypeDto>(x => x.Alias == contentTypeAlias);
            }

            return Database.ExecuteScalar<int>(sql);
        }

        public int CountChildren(int parentId, string contentTypeAlias = null)
        {
            var sql = Sql()
                .SelectCount()
                .From<NodeDto>();

            if (contentTypeAlias.IsNullOrWhiteSpace())
            {
                sql
                    .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId)
                    .Where<NodeDto>(x => x.ParentId == parentId);
            }
            else
            {
                sql
                    .InnerJoin<ContentDto>()
                    .On<NodeDto, ContentDto>(left => left.NodeId, right => right.NodeId)
                    .InnerJoin<ContentTypeDto>()
                    .On<ContentTypeDto, ContentDto>(left => left.NodeId, right => right.ContentTypeId)
                    .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId)
                    .Where<NodeDto>(x => x.ParentId == parentId)
                    .Where<ContentTypeDto>(x => x.Alias == contentTypeAlias);
            }

            return Database.ExecuteScalar<int>(sql);
        }

        /// <summary>
        /// Get the total count of entities
        /// </summary>
        /// <param name="contentTypeAlias"></param>
        /// <returns></returns>
        public int Count(string contentTypeAlias = null)
        {
            var sql = Sql()
                .SelectCount()
                .From<NodeDto>();

            if (contentTypeAlias.IsNullOrWhiteSpace())
            {
                sql
                    .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId);
            }
            else
            {
                sql
                    .InnerJoin<ContentDto>()
                    .On<NodeDto, ContentDto>(left => left.NodeId, right => right.NodeId)
                    .InnerJoin<ContentTypeDto>()
                    .On<ContentTypeDto, ContentDto>(left => left.NodeId, right => right.ContentTypeId)
                    .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId)
                    .Where<ContentTypeDto>(x => x.Alias == contentTypeAlias);
            }

            return Database.ExecuteScalar<int>(sql);
        }

        /// <summary>
        /// This removes associated tags from the entity - used generally when an entity is recycled
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="tagRepo"></param>
        protected void ClearEntityTags(IContentBase entity, ITagRepository tagRepo)
        {
            tagRepo.ClearTagsFromEntity(entity.Id);
        }

        /// <summary>
        /// Updates the tag repository with any tag enabled properties and their values
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="tagRepo"></param>
        protected void UpdateEntityTags(IContentBase entity, ITagRepository tagRepo)
        {
            foreach (var tagProp in entity.Properties.Where(x => x.TagSupport.Enable))
            {
                if (tagProp.TagSupport.Behavior == PropertyTagBehavior.Remove)
                {
                    //remove the specific tags
                    tagRepo.RemoveTagsFromProperty(
                        entity.Id,
                        tagProp.PropertyTypeId,
                        tagProp.TagSupport.Tags.Select(x => new Tag { Text = x.Item1, Group = x.Item2 }));
                }
                else
                {
                    //assign the tags
                    tagRepo.AssignTagsToProperty(
                        entity.Id,
                        tagProp.PropertyTypeId,
                        tagProp.TagSupport.Tags.Select(x => new Tag { Text = x.Item1, Group = x.Item2 }),
                        tagProp.TagSupport.Behavior == PropertyTagBehavior.Replace);
                }
            }
        }

        protected bool HasTagProperty(IContentBase entity)
        {
            return entity.Properties.Any(x => x.TagSupport.Enable);
        }

        private Sql<SqlContext> PrepareSqlForPagedResults(Sql<SqlContext> sql, Sql<SqlContext> filterSql, string orderBy, Direction orderDirection, bool orderBySystemField, string table)
        {
            if (filterSql == null && string.IsNullOrEmpty(orderBy)) return sql;

            // preserve original
            var psql = new Sql<SqlContext>(sql.SqlContext, sql.SQL, sql.Arguments);

            // apply filter
            if (filterSql != null)
                psql.Append(filterSql);

            // non-sorting, we're done
            if (string.IsNullOrEmpty(orderBy))
                return psql;

            // else apply sort
            var dbfield = orderBySystemField
                ? GetOrderBySystemField(ref psql, orderBy)
                : GetOrderByNonSystemField(ref psql, orderBy, table);

            if (orderDirection == Direction.Ascending)
                psql.OrderBy(dbfield);
            else
                psql.OrderByDescending(dbfield);

            // no matter what we always MUST order the result also by umbracoNode.id to ensure that all records being ordered by are unique.
            // if we do not do this then we end up with issues where we are ordering by a field that has duplicate values (i.e. the 'text' column
            // is empty for many nodes)
            // see: http://issues.umbraco.org/issue/U4-8831
            dbfield = GetDatabaseFieldNameForOrderBy("umbracoNode", "id");
            var matches = VersionableRepositoryBaseAliasRegex.For(SqlSyntax).Matches(sql.SQL);
            var match = matches.Cast<Match>().FirstOrDefault(m => m.Groups[1].Value.InvariantEquals(dbfield));
            if (match != null)
                dbfield = match.Groups[2].Value;
            psql.OrderBy(dbfield);

            // fixme - temp - for the time being NPoco PagingHelper cannot deal with multiline
            psql = new Sql<SqlContext>(psql.SqlContext, psql.SQL.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "), psql.Arguments);

            return psql;
        }

        private string GetOrderBySystemField(ref Sql<SqlContext> sql, string orderBy)
        {
            // get the database field eg "[table].[column]"
            var dbfield = GetDatabaseFieldNameForOrderBy(orderBy);

            // for SqlServer pagination to work, the "order by" field needs to be the alias eg if
            // the select statement has "umbracoNode.text AS NodeDto__Text" then the order field needs
            // to be "NodeDto__Text" and NOT "umbracoNode.text".
            // not sure about SqlCE nor MySql, so better do it too. initially thought about patching
            // NPoco but that would be expensive and not 100% possible, so better give NPoco proper
            // queries to begin with.
            // thought about maintaining a map of columns-to-aliases in the sql context but that would
            // be expensive and most of the time, useless. so instead we parse the SQL looking for the
            // alias. somewhat expensive too but nothing's free.

            // note: ContentTypeAlias is not properly managed because it's not part of the query to begin with!

            var matches = VersionableRepositoryBaseAliasRegex.For(SqlSyntax).Matches(sql.SQL);
            var match = matches.Cast<Match>().FirstOrDefault(m => m.Groups[1].Value.InvariantEquals(dbfield));
            if (match != null)
                dbfield = match.Groups[2].Value;

            return dbfield;
        }

        private string GetOrderByNonSystemField(ref Sql<SqlContext> sql, string orderBy, string table)
        {
            // Sorting by a custom field, so set-up sub-query for ORDER BY clause to pull through value
            // from most recent content version for the given order by field
            var sortedInt = string.Format(SqlSyntax.ConvertIntegerToOrderableString, "dataInt");
            var sortedDate = string.Format(SqlSyntax.ConvertDateToOrderableString, "dataDate");
            var sortedString = "COALESCE(dataNvarchar,'')"; // assuming COALESCE is ok for all syntaxes
            var sortedDecimal = string.Format(SqlSyntax.ConvertDecimalToOrderableString, "dataDecimal");

            // variable query fragments that depend on what we are querying
            string andVersion, andNewest, idField;
            switch (table)
            {
                case "cmsDocument":
                    andVersion = " AND cpd.versionId = cd.versionId";
                    andNewest = " AND cd.newest = 1";
                    idField = "nodeId";
                    break;
                case "cmsMember":
                    andVersion = string.Empty;
                    andNewest = string.Empty;
                    idField = "nodeId";
                    break;
                case "cmsContentVersion":
                    andVersion = " AND cpd.versionId = cd.versionId";
                    andNewest = string.Empty;
                    idField = "contentId";
                    break;
                default:
                    throw new NotSupportedException($"Table {table} is not supported.");
            }

            // needs to be an outer join since there's no guarantee that any of the nodes have values for this property
            var outerJoinTempTable = $@"LEFT OUTER JOIN (
                    SELECT CASE
                        WHEN dataInt IS NOT NULL THEN {sortedInt}
                        WHEN dataDecimal IS NOT NULL THEN {sortedDecimal}
                        WHEN dataDate IS NOT NULL THEN {sortedDate}
                        ELSE {sortedString}
                    END AS CustomPropVal,
                    cd.{idField} AS CustomPropValContentId
                    FROM {table} cd
                    INNER JOIN cmsPropertyData cpd ON cpd.contentNodeId = cd.{idField}{andVersion}
                    INNER JOIN cmsPropertyType cpt ON cpt.Id = cpd.propertytypeId
                    WHERE cpt.Alias = @{sql.Arguments.Length}{andNewest}) AS CustomPropData
                    ON CustomPropData.CustomPropValContentId = umbracoNode.id "; // trailing space is important!

            // insert this just above the first LEFT OUTER JOIN (for cmsDocument) or the last WHERE (everything else)
            string newSql;
            if (table == "cmsDocument")
            {
                // insert the SQL fragment just above the LEFT OUTER JOIN [cmsDocument] [cmsDocument2] ...
                // ensure it's there, 'cos, someone's going to edit the query, inevitably!
                var pos = sql.SQL.InvariantIndexOf("LEFT OUTER JOIN");
                if (pos < 0) throw new Exception("Oops, LEFT OUTER JOIN not found.");
                newSql = sql.SQL.Insert(pos, outerJoinTempTable);
            }
            else // anything else (see above)
            {
                // else same above WHERE
                var pos = sql.SQL.InvariantIndexOf("WHERE");
                if (pos < 0) throw new Exception("Oops, WHERE not found.");
                newSql = sql.SQL.Insert(pos, outerJoinTempTable);
            }

            var newArgs = sql.Arguments.ToList();
            newArgs.Add(orderBy);

            // insert the SQL selected field, too, else ordering cannot work
            if (sql.SQL.StartsWith("SELECT ") == false) throw new Exception("Oops, SELECT not found.");
            newSql = newSql.Insert("SELECT ".Length, "CustomPropData.CustomPropVal, ");

            sql = new Sql<SqlContext>(sql.SqlContext, newSql, newArgs.ToArray());

            // and order by the custom field
            return "CustomPropData.CustomPropVal";
        }

        protected IEnumerable<TEntity> GetPagedResultsByQuery<TDto>(IQuery<TEntity> query, long pageIndex, int pageSize, out long totalRecords,
            Func<List<TDto>, IEnumerable<TEntity>> mapper,
            string orderBy, Direction orderDirection, bool orderBySystemField, string table,
            Sql<SqlContext> filterSql = null)
        {
            if (orderBy == null) throw new ArgumentNullException(nameof(orderBy));

            // start with base query, and apply the supplied IQuery
            var sqlBase = GetBaseQuery(false);
            if (query == null) query = Query;
            var translator = new SqlTranslator<TEntity>(sqlBase, query);
            var sqlNodeIds = translator.Translate();

            // sort and filter
            sqlNodeIds = PrepareSqlForPagedResults(sqlNodeIds, filterSql, orderBy, orderDirection, orderBySystemField, table);

            // get a page of DTOs and the total count
            var pagedResult = Database.Page<TDto>(pageIndex + 1, pageSize, sqlNodeIds);
            totalRecords = Convert.ToInt32(pagedResult.TotalItems);

            // map the DTOs and return
            return mapper(pagedResult.Items);
        }

        protected IDictionary<int, PropertyCollection> GetPropertyCollection(DocumentDefinition[] ddefs)
        {
            var versions = ddefs.Select(x => x.Version).ToArray();
            if (versions.Length == 0) return new Dictionary<int, PropertyCollection>();

            // fetch by version only, that should be enough, versions are guids and the same guid
            // should not be reused for two different nodes -- then validate with a Where() just
            // to be sure -- but we probably can get rid of the validation
            var allPropertyData = Database.FetchByGroups<PropertyDataDto, Guid>(versions, 2000, batch =>
                Sql()
                    .Select<PropertyDataDto>(r => r.Select<PropertyTypeDto>())
                    .From<PropertyDataDto>()
                    .LeftJoin<PropertyTypeDto>()
                    .On<PropertyDataDto, PropertyTypeDto>(left => left.PropertyTypeId, right => right.Id)
                    .WhereIn<PropertyDataDto>(x => x.VersionId, batch))
                .Where(x => ddefs.Any(y => y.Version == x.VersionId && y.Id == x.NodeId)) // so... probably redundant, but safe
                .ToList();

            // lazy access to prevalue for data types if any property requires tag support
            var pre = new Lazy<IEnumerable<DataTypePreValueDto>>(() =>
            {
                var allPropertyTypes = allPropertyData
                    .Select(x => x.PropertyTypeDto.Id)
                    .Distinct();

                var allDataTypePreValue = Database.FetchByGroups<DataTypePreValueDto, int>(allPropertyTypes, 2000, batch =>
                    Sql()
                        .Select<DataTypePreValueDto>()
                        .From<DataTypePreValueDto>()
                        .LeftJoin<PropertyTypeDto>().On<DataTypePreValueDto, PropertyTypeDto>(left => left.DataTypeNodeId, right => right.DataTypeId)
                        .WhereIn<PropertyTypeDto>(x => x.Id, batch));

                return allDataTypePreValue;
            });

            return GetPropertyCollection(ddefs, allPropertyData, pre);
        }

        protected IDictionary<int, PropertyCollection> GetPropertyCollection(DocumentDefinition[] documentDefs, List<PropertyDataDto> allPropertyData, Lazy<IEnumerable<DataTypePreValueDto>> allPreValues)
        {
            var result = new Dictionary<int, PropertyCollection>();

            var propertiesWithTagSupport = new Dictionary<string, SupportTagsAttribute>();

            //iterate each definition grouped by it's content type - this will mean less property type iterations while building
            // up the property collections
            foreach (var compositionGroup in documentDefs.GroupBy(x => x.Composition))
            {
                var compositionProperties = compositionGroup.Key.CompositionPropertyTypes.ToArray();

                foreach (var def in compositionGroup)
                {
                    var propertyDataDtos = allPropertyData.Where(x => x.NodeId == def.Id).Distinct();

                    var propertyFactory = new PropertyFactory(compositionProperties, def.Version, def.Id, def.CreateDate, def.VersionDate);
                    var properties = propertyFactory.BuildEntity(propertyDataDtos.ToArray()).ToArray();

                    foreach (var property in properties)
                    {
                        //NOTE: The benchmarks run with and without the following code show very little change so this is not a perf bottleneck
                        var editor = Current.PropertyEditors[property.PropertyType.PropertyEditorAlias];

                        var tagSupport = propertiesWithTagSupport.ContainsKey(property.PropertyType.PropertyEditorAlias)
                            ? propertiesWithTagSupport[property.PropertyType.PropertyEditorAlias]
                            : TagExtractor.GetAttribute(editor);

                        if (tagSupport != null)
                        {
                            //add to local cache so we don't need to reflect next time for this property editor alias
                            propertiesWithTagSupport[property.PropertyType.PropertyEditorAlias] = tagSupport;

                            //this property has tags, so we need to extract them and for that we need the prevals which we've already looked up
                            var preValData = allPreValues.Value.Where(x => x.DataTypeNodeId == property.PropertyType.DataTypeDefinitionId)
                                .Distinct()
                                .ToArray();

                            var asDictionary = preValData.ToDictionary(x => x.Alias, x => new PreValue(x.Id, x.Value, x.SortOrder));

                            var preVals = new PreValueCollection(asDictionary);

                            var contentPropData = new ContentPropertyData(property.Value,
                                preVals,
                                new Dictionary<string, object>());

                            TagExtractor.SetPropertyTags(property, contentPropData, property.Value, tagSupport);
                        }
                    }

                    if (result.ContainsKey(def.Id))
                    {
                        Logger.Warn<VersionableRepositoryBase<TId, TEntity, TRepository>>("The query returned multiple property sets for document definition " + def.Id + ", " + def.Composition.Name);
                    }
                    result[def.Id] = new PropertyCollection(properties);
                }
            }

            return result;
        }

        public class DocumentDefinition
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="T:System.Object"/> class.
            /// </summary>
            public DocumentDefinition(int id, Guid version, DateTime versionDate, DateTime createDate, IContentTypeComposition composition)
            {
                Id = id;
                Version = version;
                VersionDate = versionDate;
                CreateDate = createDate;
                Composition = composition;
            }

            public int Id { get; set; }
            public Guid Version { get; set; }
            public DateTime VersionDate { get; set; }
            public DateTime CreateDate { get; set; }
            public IContentTypeComposition Composition { get; set; }
        }

        protected virtual string GetDatabaseFieldNameForOrderBy(string orderBy)
        {
            // translate the supplied "order by" field, which were originally defined for in-memory
            // object sorting of ContentItemBasic instance, to the actual database field names.

            switch (orderBy.ToUpperInvariant())
            {
                case "VERSIONDATE":
                case "UPDATEDATE":
                    return GetDatabaseFieldNameForOrderBy("cmsContentVersion", "versionDate");
                case "CREATEDATE":
                    return GetDatabaseFieldNameForOrderBy("umbracoNode", "createDate");
                case "NAME":
                    return GetDatabaseFieldNameForOrderBy("umbracoNode", "text");
                case "PUBLISHED":
                    return GetDatabaseFieldNameForOrderBy("cmsDocument", "published");
                case "OWNER":
                    //TODO: This isn't going to work very nicely because it's going to order by ID, not by letter
                    return GetDatabaseFieldNameForOrderBy("umbracoNode", "nodeUser");
                case "PATH":
                    return GetDatabaseFieldNameForOrderBy("umbracoNode", "path");
                case "SORTORDER":
                    return GetDatabaseFieldNameForOrderBy("umbracoNode", "sortOrder");
                default:
                    //ensure invalid SQL cannot be submitted
                    return Regex.Replace(orderBy, @"[^\w\.,`\[\]@-]", "");
            }
        }

        protected string GetDatabaseFieldNameForOrderBy(string tableName, string fieldName)
        {
            return SqlSyntax.GetQuotedTableName(tableName) + "." + SqlSyntax.GetQuotedColumnName(fieldName);
        }

        #region UnitOfWork Events

        public class UnitOfWorkEntityEventArgs : EventArgs
        {
            public UnitOfWorkEntityEventArgs(IDatabaseUnitOfWork unitOfWork, TEntity entity)
            {
                UnitOfWork = unitOfWork;
                Entity = entity;
            }

            public IDatabaseUnitOfWork UnitOfWork { get; }

            public TEntity Entity { get; }
        }

        public class UnitOfWorkVersionEventArgs : EventArgs
        {
            public UnitOfWorkVersionEventArgs(IDatabaseUnitOfWork unitOfWork, int entityId, Guid versionId)
            {
                UnitOfWork = unitOfWork;
                EntityId = entityId;
                VersionId = versionId;
            }

            public IDatabaseUnitOfWork UnitOfWork { get; private set; }

            public int EntityId { get; }

            public Guid VersionId { get; }
        }

        public static event TypedEventHandler<TRepository, UnitOfWorkEntityEventArgs> UowRefreshedEntity;
        public static event TypedEventHandler<TRepository, UnitOfWorkEntityEventArgs> UowRemovingEntity;
        public static event TypedEventHandler<TRepository, UnitOfWorkVersionEventArgs> UowRemovingVersion;

        protected void OnUowRefreshedEntity(UnitOfWorkEntityEventArgs args)
        {
            UowRefreshedEntity.RaiseEvent(args, Instance);
        }

        protected void OnUowRemovingEntity(UnitOfWorkEntityEventArgs args)
        {
            UowRemovingEntity.RaiseEvent(args, Instance);
        }

        protected void OnUowRemovingVersion(UnitOfWorkVersionEventArgs args)
        {
            UowRemovingVersion.RaiseEvent(args, Instance);
        }

        #endregion
    }
}