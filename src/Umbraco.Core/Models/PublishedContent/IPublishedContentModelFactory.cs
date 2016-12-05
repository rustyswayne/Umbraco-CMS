﻿namespace Umbraco.Core.Models.PublishedContent
{
    /// <summary>
    /// Provides the model creation service.
    /// </summary>
    public interface IPublishedContentModelFactory
    {
        /// <summary>
        /// Creates a strongly-typed model representing a published content.
        /// </summary>
        /// <param name="content">The original published content.</param>
        /// <returns>The strongly-typed model representing the published content, or the published content
        /// itself it the factory has no model for that content type.</returns>
        IPublishedContent CreateModel(IPublishedContent content);

        T CreateModel<T>(IPublishedFragment content);

        // fixme
        // and we'd need a
        // PublishedContentModel = ContentModel : ContentWrapper
        // PublishedFragmentModel = FragmentModel : FragmentWrapper
        //
        // ModelFactory.Meta.Model("thing").ClrType (find the our post?)
        //
        // then
        // make a plan to get NestedContent in
        // and an equivalent of Vorto with different syntax
        //
        // then
        // VARIANTS ARCHITECTURE FFS!
    }
}