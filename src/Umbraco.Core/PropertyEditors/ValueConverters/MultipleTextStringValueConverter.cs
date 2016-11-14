﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Umbraco.Core.Models.PublishedContent;

namespace Umbraco.Core.PropertyEditors.ValueConverters
{
    public class MultipleTextStringValueConverter : PropertyValueConverterBase
    {
        public override bool IsConverter(PublishedPropertyType propertyType)
        {
            return Constants.PropertyEditors.MultipleTextstringAlias.Equals(propertyType.PropertyEditorAlias);
        }

        public override Type GetPropertyValueType(PublishedPropertyType propertyType)
        {
            return typeof (IEnumerable<string>);
        }

        public override PropertyCacheLevel GetPropertyCacheLevel(PublishedPropertyType propertyType)
        {
            return PropertyCacheLevel.Content;
        }

        public override object ConvertSourceToInter(PublishedPropertyType propertyType, object source, bool preview)
        {
            // data is (both in database and xml):
            // <keyFeatureList>
            //    <values>
            //        <value>Strong</value>
            //        <value>Flexible</value>
            //        <value>Efficient</value>
            //    </values>
            // </keyFeatureList>

            var sourceString = source != null ? source.ToString() : null;
            if (string.IsNullOrWhiteSpace(sourceString)) return Enumerable.Empty<string>();

            //SD: I have no idea why this logic is here, I'm pretty sure we've never saved the multiple txt string
            // as xml in the database, it's always been new line delimited. Will ask Stephen about this.
            // In the meantime, we'll do this xml check, see if it parses and if not just continue with
            // splitting by newline
            //
            //   RS: SD/Stephan Please consider post before deciding to remove
            //// https://our.umbraco.org/forum/contributing-to-umbraco-cms/76989-keep-the-xml-values-in-the-multipletextstringvalueconverter
            var values = new List<string>();
            var pos = sourceString.IndexOf("<value>", StringComparison.Ordinal);
            while (pos >= 0)
            {
                pos += "<value>".Length;
                var npos = sourceString.IndexOf("<", pos, StringComparison.Ordinal);
                var value = sourceString.Substring(pos, npos - pos);
                values.Add(value);
                pos = sourceString.IndexOf("<value>", pos, StringComparison.Ordinal);
            }

            // Fall back on normal behaviour
            if (values.Any() == false)
            {
                return sourceString.Split(Environment.NewLine.ToCharArray());
            }

            return values.ToArray();
        }

        public override object ConvertInterToXPath(PublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object inter, bool preview)
        {
            var d = new XmlDocument();
            var e = d.CreateElement("values");
            d.AppendChild(e);

            var values = (IEnumerable<string>) inter;
            foreach (var value in values)
            {
                var ee = d.CreateElement("value");
                ee.InnerText = value;
                e.AppendChild(ee);
            }

            return d.CreateNavigator();
        }
    }
}
