﻿using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using AutoMapper;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Mapping;
using Umbraco.Core.Services;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Trees;

namespace Umbraco.Web.Models.Mapping
{
    /// <summary>
    /// Declares model mappings for media.
    /// </summary>
    internal class MediaModelMapper : ModelMapperConfiguration
    {
        private readonly ILocalizedTextService _textService;
        private readonly IUserService _userService;
        private readonly IDataTypeService _dataTypeService;
        private readonly IMediaService _mediaService;
        private readonly ILogger _logger;

        public MediaModelMapper(IUserService userService, ILocalizedTextService textService, IDataTypeService dataTypeService, IMediaService mediaService, ILogger logger)
        {
            _userService = userService;
            _textService = textService;
            _dataTypeService = dataTypeService;
            _mediaService = mediaService;
            _logger = logger;
        }

        public override void ConfigureMappings(IMapperConfiguration config)
        {
            //FROM IMedia TO MediaItemDisplay
            config.CreateMap<IMedia, MediaItemDisplay>()
                .ForMember(
                    dto => dto.Owner,
                    expression => expression.ResolveUsing(new OwnerResolver<IMedia>(_userService)))
                .ForMember(
                    dto => dto.Icon,
                    expression => expression.MapFrom(content => content.ContentType.Icon))
                .ForMember(
                    dto => dto.ContentTypeAlias,
                    expression => expression.MapFrom(content => content.ContentType.Alias))
                .ForMember(display => display.IsChildOfListView, expression => expression.Ignore())
                .ForMember(
                    dto => dto.Trashed,
                    expression => expression.MapFrom(content => content.Trashed))
                .ForMember(
                    dto => dto.ContentTypeName,
                    expression => expression.MapFrom(content => content.ContentType.Name))
                .ForMember(display => display.Properties, expression => expression.Ignore())
                .ForMember(display => display.TreeNodeUrl, expression => expression.Ignore())
                .ForMember(display => display.Notifications, expression => expression.Ignore())
                .ForMember(display => display.Errors, expression => expression.Ignore())
                .ForMember(display => display.Published, expression => expression.Ignore())
                .ForMember(display => display.Updater, expression => expression.Ignore())
                .ForMember(display => display.Alias, expression => expression.Ignore())
                .ForMember(display => display.IsContainer, expression => expression.Ignore())
                .ForMember(member => member.HasPublishedVersion, expression => expression.Ignore())
                .ForMember(display => display.Tabs, expression => expression.ResolveUsing(new TabsAndPropertiesResolver(_textService)))
                .AfterMap((media, display) => AfterMap(media, display, _dataTypeService, _textService, _logger, _mediaService));

            //FROM IMedia TO ContentItemBasic<ContentPropertyBasic, IMedia>
            config.CreateMap<IMedia, ContentItemBasic<ContentPropertyBasic, IMedia>>()
                .ForMember(
                    dto => dto.Owner,
                    expression => expression.ResolveUsing(new OwnerResolver<IMedia>(_userService)))
                .ForMember(
                    dto => dto.Icon,
                    expression => expression.MapFrom(content => content.ContentType.Icon))
                .ForMember(
                    dto => dto.Trashed,
                    expression => expression.MapFrom(content => content.Trashed))
                .ForMember(
                    dto => dto.ContentTypeAlias,
                    expression => expression.MapFrom(content => content.ContentType.Alias))
                .ForMember(x => x.Published, expression => expression.Ignore())
                .ForMember(x => x.Updater, expression => expression.Ignore())
                .ForMember(x => x.Alias, expression => expression.Ignore())
                .ForMember(member => member.HasPublishedVersion, expression => expression.Ignore());

            //FROM IMedia TO ContentItemDto<IMedia>
            config.CreateMap<IMedia, ContentItemDto<IMedia>>()
                .ForMember(
                    dto => dto.Owner,
                    expression => expression.ResolveUsing(new OwnerResolver<IMedia>(_userService)))
                .ForMember(x => x.Published, expression => expression.Ignore())
                .ForMember(x => x.Updater, expression => expression.Ignore())
                .ForMember(x => x.Icon, expression => expression.Ignore())
                .ForMember(x => x.Alias, expression => expression.Ignore())
                .ForMember(member => member.HasPublishedVersion, expression => expression.Ignore());
        }

        private static void AfterMap(IMedia media, MediaItemDisplay display, IDataTypeService dataTypeService, ILocalizedTextService localizedText, ILogger logger, IMediaService mediaService)
        {
			// Adapted from ContentModelMapper
			//map the IsChildOfListView (this is actually if it is a descendant of a list view!)
            //TODO: Fix this shorthand .Ancestors() lookup, at least have an overload to use the current
            if (media.HasIdentity)
            {
                var ancesctorListView = media.Ancestors(mediaService).FirstOrDefault(x => x.ContentType.IsContainer);
                display.IsChildOfListView = ancesctorListView != null;
            }
            else
            {
                //it's new so it doesn't have a path, so we need to look this up by it's parent + ancestors
                var parent = media.Parent(mediaService);
                if (parent == null)
                {
                    display.IsChildOfListView = false;
                }
                else if (parent.ContentType.IsContainer)
                {
                    display.IsChildOfListView = true;
                }
                else
                {
                    var ancesctorListView = parent.Ancestors().FirstOrDefault(x => x.ContentType.IsContainer);
                    display.IsChildOfListView = ancesctorListView != null;
                }
            }
			
            //map the tree node url
            if (HttpContext.Current != null)
            {
                var urlHelper = new UrlHelper(new RequestContext(new HttpContextWrapper(HttpContext.Current), new RouteData()));
                var url = urlHelper.GetUmbracoApiService<MediaTreeController>(controller => controller.GetTreeNode(display.Id.ToString(), null));
                display.TreeNodeUrl = url;
            }
            
            if (media.ContentType.IsContainer)
            {
                TabsAndPropertiesResolver.AddListView(display, "media", dataTypeService, localizedText);
            }
            
            var genericProperties = new List<ContentPropertyDisplay>
            {
                new ContentPropertyDisplay
                {
                    Alias = string.Format("{0}doctype", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                    Label = localizedText.Localize("content/mediatype"),
                    Value = localizedText.UmbracoDictionaryTranslate(display.ContentTypeName),
                    View = Current.PropertyEditors[Constants.PropertyEditors.NoEditAlias].ValueEditor.View
                }
            };

            var links = media.GetUrls(UmbracoConfig.For.UmbracoSettings().Content, logger);

            if (links.Any())
            {
                var link = new ContentPropertyDisplay
                {
                    Alias = string.Format("{0}urls", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                    Label = localizedText.Localize("media/urls"),
                    Value = string.Join(",", links),
                    View = "urllist"
                };
                genericProperties.Add(link);
            }

            TabsAndPropertiesResolver.MapGenericProperties(media, display, localizedText, genericProperties);
        }

    }
}
