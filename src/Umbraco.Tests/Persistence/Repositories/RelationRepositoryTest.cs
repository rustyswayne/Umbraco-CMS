﻿using System;
using System.Linq;
using Moq;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence;

using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.TestHelpers.Entities;

namespace Umbraco.Tests.Persistence.Repositories
{
    [TestFixture]
    [UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
    public class RelationRepositoryTest : TestWithDatabaseBase
    {
        public override void SetUp()
        {
            base.SetUp();

            CreateTestData();
        }

        private RelationRepository CreateRepository(IDatabaseUnitOfWork unitOfWork, out RelationTypeRepository relationTypeRepository)
        {
            relationTypeRepository = new RelationTypeRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), Mappers);
            var repository = new RelationRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), relationTypeRepository, Mappers);
            return repository;
        }

        [Test]
        public void Can_Perform_Add_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var relationType = repositoryType.Get(1);
                var relation = new Relation(NodeDto.NodeIdSeed + 2, NodeDto.NodeIdSeed + 3, relationType);
                repository.AddOrUpdate(relation);
                unitOfWork.Flush();

                // Assert
                Assert.That(relation, Is.Not.Null);
                Assert.That(relation.HasIdentity, Is.True);
            }
        }

        [Test]
        public void Can_Perform_Update_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var relation = repository.Get(1);
                relation.Comment = "This relation has been updated";
                repository.AddOrUpdate(relation);
                unitOfWork.Flush();

                var relationUpdated = repository.Get(1);

                // Assert
                Assert.That(relationUpdated, Is.Not.Null);
                Assert.That(relationUpdated.Comment, Is.EqualTo("This relation has been updated"));
                Assert.AreNotEqual(relationUpdated.UpdateDate, relation.UpdateDate);
            }
        }

        [Test]
        public void Can_Perform_Delete_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var relation = repository.Get(2);
                repository.Delete(relation);
                unitOfWork.Flush();

                var exists = repository.Exists(2);

                // Assert
                Assert.That(exists, Is.False);
            }
        }

        [Test]
        public void Can_Perform_Get_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var relation = repository.Get(1);

                // Assert
                Assert.That(relation, Is.Not.Null);
                Assert.That(relation.HasIdentity, Is.True);
                Assert.That(relation.ChildId, Is.EqualTo(NodeDto.NodeIdSeed + 2));
                Assert.That(relation.ParentId, Is.EqualTo(NodeDto.NodeIdSeed + 1));
                Assert.That(relation.RelationType.Alias, Is.EqualTo("relateContentOnCopy"));
            }
        }

        [Test]
        public void Can_Perform_GetAll_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var relations = repository.GetAll();

                // Assert
                Assert.That(relations, Is.Not.Null);
                Assert.That(relations.Any(), Is.True);
                Assert.That(relations.Any(x => x == null), Is.False);
                Assert.That(relations.Count(), Is.EqualTo(2));
            }
        }

        [Test]
        public void Can_Perform_GetAll_With_Params_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var relations = repository.GetAll(1, 2);

                // Assert
                Assert.That(relations, Is.Not.Null);
                Assert.That(relations.Any(), Is.True);
                Assert.That(relations.Any(x => x == null), Is.False);
                Assert.That(relations.Count(), Is.EqualTo(2));
            }
        }

        [Test]
        public void Can_Perform_Exists_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var exists = repository.Exists(2);
                var doesntExist = repository.Exists(5);

                // Assert
                Assert.That(exists, Is.True);
                Assert.That(doesntExist, Is.False);
            }
        }

        [Test]
        public void Can_Perform_Count_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var query = new Query<IRelation>(SqlSyntax, Mappers).Where(x => x.ParentId == NodeDto.NodeIdSeed + 1);
                int count = repository.Count(query);

                // Assert
                Assert.That(count, Is.EqualTo(2));
            }
        }

        [Test]
        public void Can_Perform_GetByQuery_On_RelationRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                // Act
                var query = new Query<IRelation>(SqlSyntax, Mappers).Where(x => x.RelationTypeId == RelationTypeDto.NodeIdSeed);
                var relations = repository.GetByQuery(query);

                // Assert
                Assert.That(relations, Is.Not.Null);
                Assert.That(relations.Any(), Is.True);
                Assert.That(relations.Any(x => x == null), Is.False);
                Assert.That(relations.Count(), Is.EqualTo(2));
            }
        }

        [Test]
        public void Can_Delete_Content_And_Verify_Relation_Is_Removed()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                RelationTypeRepository repositoryType;
                var repository = CreateRepository(unitOfWork, out repositoryType);

                var content = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 2);
                ServiceContext.ContentService.Delete(content, 0);

                // Act
                var shouldntExist = repository.Exists(1);
                var shouldExist = repository.Exists(2);

                // Assert
                Assert.That(shouldntExist, Is.False);
                Assert.That(shouldExist, Is.True);
            }
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        public void CreateTestData()
        {
            var relateContent = new RelationType(new Guid(Constants.ObjectTypes.Document), new Guid("C66BA18E-EAF3-4CFF-8A22-41B16D66A972"), "relateContentOnCopy") { IsBidirectional = true, Name = "Relate Content on Copy" };
            var relateContentType = new RelationType(new Guid(Constants.ObjectTypes.DocumentType), new Guid("A2CB7800-F571-4787-9638-BC48539A0EFB"), "relateContentTypeOnCopy") { IsBidirectional = true, Name = "Relate ContentType on Copy" };

            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var relationTypeRepository = new RelationTypeRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), Mappers);
                var relationRepository = new RelationRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), relationTypeRepository, Mappers);

                relationTypeRepository.AddOrUpdate(relateContent);
                relationTypeRepository.AddOrUpdate(relateContentType);
                unitOfWork.Flush();

                //Create and Save ContentType "umbTextpage" -> (NodeDto.NodeIdSeed)
                ContentType contentType = MockedContentTypes.CreateSimpleContentType("umbTextpage", "Textpage");
                ServiceContext.ContentTypeService.Save(contentType);

                //Create and Save Content "Homepage" based on "umbTextpage" -> (NodeDto.NodeIdSeed + 1)
                Content textpage = MockedContent.CreateSimpleContent(contentType);
                ServiceContext.ContentService.Save(textpage, 0);

                //Create and Save Content "Text Page 1" based on "umbTextpage" -> (NodeDto.NodeIdSeed + 2)
                Content subpage = MockedContent.CreateSimpleContent(contentType, "Text Page 1", textpage.Id);
                ServiceContext.ContentService.Save(subpage, 0);

                //Create and Save Content "Text Page 1" based on "umbTextpage" -> (NodeDto.NodeIdSeed + 3)
                Content subpage2 = MockedContent.CreateSimpleContent(contentType, "Text Page 2", textpage.Id);
                ServiceContext.ContentService.Save(subpage2, 0);

                var relation = new Relation(textpage.Id, subpage.Id, relateContent) { Comment = string.Empty };
                var relation2 = new Relation(textpage.Id, subpage2.Id, relateContent) { Comment = string.Empty };
                relationRepository.AddOrUpdate(relation);
                relationRepository.AddOrUpdate(relation2);
                unitOfWork.Complete();
            }
        }
    }
}