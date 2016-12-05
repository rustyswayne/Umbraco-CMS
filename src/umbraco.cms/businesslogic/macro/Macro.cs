using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using umbraco.DataLayer;
using umbraco.BusinessLogic;
using System.Linq;
using Umbraco.Core.DI;
using Umbraco.Core.Xml;

namespace umbraco.cms.businesslogic.macro
{
	/// <summary>
	/// The Macro component are one of the umbraco essentials, used for drawing dynamic content in the public website of umbraco.
	///
	/// A Macro is a placeholder for either a xsl transformation, a custom .net control or a .net usercontrol.
	///
	/// The Macro is representated in templates and content as a special html element, which are being parsed out and replaced with the
	/// output of either the .net control or the xsl transformation when a page is being displayed to the visitor.
	///
	/// A macro can have a variety of properties which are used to transfer userinput to either the usercontrol/custom control or the xsl
	///
	/// </summary>
    [Obsolete("This is no longer used, use the IMacroService and related models instead")]
	public class Macro
	{
        //initialize empty model
	    internal IMacro MacroEntity = new Umbraco.Core.Models.Macro();

        protected static ISqlHelper SqlHelper
        {
            get { return LegacySqlHelper.SqlHelper; }
        }

		/// <summary>
		/// id
		/// </summary>
		public int Id
		{
			get { return MacroEntity.Id; }
		}

		/// <summary>
		/// If set to true, the macro can be inserted on documents using the richtexteditor.
		/// </summary>
		public bool UseInEditor
		{
            get { return MacroEntity.UseInEditor; }
			set { MacroEntity.UseInEditor = value; }
		}

		/// <summary>
		/// The cache refreshrate - the maximum amount of time the macro should remain cached in the umbraco
		/// runtime layer.
		///
		/// The macro caches are refreshed whenever a document is changed
		/// </summary>
		public int RefreshRate
		{
            get { return MacroEntity.CacheDuration; }
			set { MacroEntity.CacheDuration = value; }
		}

        /// <summary>
		/// The alias of the macro - are used for retrieving the macro when parsing the {?UMBRACO_MACRO}{/?UMBRACO_MACRO} element,
		/// by using the alias instead of the Id, it's possible to distribute macroes from one installation to another - since the id
		/// is given by an autoincrementation in the database table, and might be used by another macro in the foreing umbraco
        /// </summary>
		public string Alias
		{
			get { return MacroEntity.Alias; }
			set { MacroEntity.Alias = value; }
		}

		/// <summary>
		/// The userfriendly name
		/// </summary>
		public string Name
		{
            get { return MacroEntity.Name; }
            set { MacroEntity.Name = value; }
		}

		/// <summary>
		/// The relative path to the usercontrol or the assembly type of the macro when using .Net custom controls
		/// </summary>
		/// <remarks>
		/// When using a user control the value is specified like: /usercontrols/myusercontrol.ascx (with the .ascx postfix)
		/// </remarks>
		public string Type
		{
            get { return MacroEntity.ControlType; }
            set { MacroEntity.ControlType = value; }
		}

		/// <summary>
		/// The xsl file used to transform content
		///
		/// Umbraco assumes that the xslfile is present in the "/xslt" folder
		/// </summary>
		public string Xslt
		{
            get { return MacroEntity.XsltPath; }
            set { MacroEntity.XsltPath = value; }
		}

	    /// <summary>
	    /// This field is used to store the file value for any scripting macro such as python, ruby, razor macros or Partial View Macros
	    /// </summary>
	    /// <remarks>
	    /// Depending on how the file is stored depends on what type of macro it is. For example if the file path is a full virtual path
	    /// starting with the ~/Views/MacroPartials then it is deemed to be a Partial View Macro, otherwise the file extension of the file
	    /// saved will determine which macro engine will be used to execute the file.
	    /// </remarks>
	    public string ScriptingFile
	    {
	        get { return MacroEntity.ScriptPath; }
            set { MacroEntity.ScriptPath = value; }
	    }

	    /// <summary>
	    /// The python file used to be executed
	    ///
	    /// Umbraco assumes that the python file is present in the "/python" folder
	    /// </summary>
	    public bool RenderContent
	    {
            get { return MacroEntity.DontRender == false; }
            set { MacroEntity.DontRender = value == false; }
	    }

	    /// <summary>
	    /// Gets or sets a value indicating whether [cache personalized].
	    /// </summary>
	    /// <value><c>true</c> if [cache personalized]; otherwise, <c>false</c>.</value>
	    public bool CachePersonalized
	    {
            get { return MacroEntity.CacheByMember; }
            set { MacroEntity.CacheByMember = value; }
	    }

	    /// <summary>
	    /// Gets or sets a value indicating whether the macro is cached for each individual page.
	    /// </summary>
	    /// <value><c>true</c> if [cache by page]; otherwise, <c>false</c>.</value>
	    public bool CacheByPage
	    {
            get { return MacroEntity.CacheByPage; }
            set { MacroEntity.CacheByPage = value; }
	    }

	    /// <summary>
	    /// Properties which are used to send parameters to the xsl/usercontrol/customcontrol of the macro
	    /// </summary>
	    public MacroProperty[] Properties
	    {
	        get
	        {
	            return MacroEntity.Properties.Select(x => new MacroProperty
	                {
	                    Alias = x.Alias,
	                    Name = x.Name,
                        SortOrder = x.SortOrder,
                        Macro = this,
                        ParameterEditorAlias = x.EditorAlias
	                }).ToArray();
	        }
	    }

        /// <summary>
        /// Macro initializer
        /// </summary>
        [Obsolete("This should no longer be used, use the IMacroService and related models instead")]
        public Macro()
		{
		}

        /// <summary>
        /// Macro initializer
        /// </summary>
        /// <param name="Id">The id of the macro</param>
        [Obsolete("This should no longer be used, use the IMacroService and related models instead")]
        public Macro(int Id)
		{
            Setup(Id);
		}

        [Obsolete("This should no longer be used, use the IMacroService and related models instead")]
        internal Macro(IMacro macro)
        {
            MacroEntity = macro;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Macro"/> class.
        /// </summary>
        /// <param name="alias">The alias.</param>
        [Obsolete("This should no longer be used, use the IMacroService and related models instead")]
        public Macro(string alias)
        {
            Setup(alias);
        }

        /// <summary>
        /// Used to persist object changes to the database. In Version3.0 it's just a stub for future compatibility
        /// </summary>
        [Obsolete("This should no longer be used, use the IMacroService and related models instead")]
        public virtual void Save()
	    {
            Current.Services.MacroService.Save(MacroEntity);
        }

	    /// <summary>
		/// Deletes the current macro
		/// </summary>
		public void Delete()
		{
            Current.Services.MacroService.Delete(MacroEntity);
        }

        [Obsolete("This is no longer used, use the IMacroService and related models instead")]
        public static Macro Import(XmlNode n)
        {
            var alias = XmlHelper.GetNodeValue(n.SelectSingleNode("alias"));
            //check to see if the macro alreay exists in the system
            //it's better if it does and we keep using it, alias *should* be unique remember

            var m = Macro.GetByAlias(alias);


            if (m == null)
            {
                m = MakeNew(XmlHelper.GetNodeValue(n.SelectSingleNode("name")));
            }
            try
            {
                m.Alias = alias;
                m.Type = XmlHelper.GetNodeValue(n.SelectSingleNode("scriptType"));
                m.Xslt = XmlHelper.GetNodeValue(n.SelectSingleNode("xslt"));
                m.RefreshRate = int.Parse(XmlHelper.GetNodeValue(n.SelectSingleNode("refreshRate")));

                // we need to validate if the usercontrol is missing the tilde prefix requirement introduced in v6
                if (string.IsNullOrEmpty(m.Type) == false && m.Type.StartsWith("~") == false)
                {
                    m.Type = "~/" + m.Type;
                }

                if (n.SelectSingleNode("scriptingFile") != null)
                {
                    m.ScriptingFile = XmlHelper.GetNodeValue(n.SelectSingleNode("scriptingFile"));
                }

                try
                {
                    m.UseInEditor = bool.Parse(XmlHelper.GetNodeValue(n.SelectSingleNode("useInEditor")));
                }
                catch (Exception macroExp)
                {
                    Current.Logger.Error<Macro>("Error creating macro property", macroExp);
                }

                // macro properties
                foreach (XmlNode mp in n.SelectNodes("properties/property"))
                {
                    try
                    {
                        var propertyAlias = mp.Attributes.GetNamedItem("alias").Value;
                        var property = m.Properties.SingleOrDefault(p => p.Alias == propertyAlias);
                        if (property != null)
                        {
                            property.Name = mp.Attributes.GetNamedItem("name").Value;
                            property.ParameterEditorAlias = mp.Attributes.GetNamedItem("propertyType").Value;

                            property.Save();
                        }
                        else
                        {
                            MacroProperty.MakeNew(
                                m,
                                propertyAlias,
                                mp.Attributes.GetNamedItem("name").Value,
                                mp.Attributes.GetNamedItem("propertyType").Value
                                );
                        }
                    }
                    catch (Exception macroPropertyExp)
                    {
                        Current.Logger.Error<Macro>("Error creating macro property", macroPropertyExp);
                    }
                }

                m.Save();
            }
            catch (Exception ex)
            {
                Current.Logger.Error<Macro>("An error occurred importing a macro", ex);
                return null;
            }

            return m;
        }

		private void Setup(int id)
		{
            var macro = Current.Services.MacroService.GetById(id);

            if (macro == null)
                throw new ArgumentException(string.Format("No Macro exists with id '{0}'", id));

		    MacroEntity = macro;
		}

        private void Setup(string alias)
        {
            var macro = Current.Services.MacroService.GetByAlias(alias);

            if (macro == null)
                throw new ArgumentException(string.Format("No Macro exists with alias '{0}'", alias));

            MacroEntity = macro;
        }

	    /// <summary>
	    /// Get an xmlrepresentation of the macro, used for exporting the macro to a package for distribution
	    /// </summary>
	    /// <param name="xd">Current xmldocument context</param>
	    /// <returns>An xmlrepresentation of the macro</returns>
	    public XmlNode ToXml(XmlDocument xd)
	    {
            var serializer = new EntityXmlSerializer();
            var xml = serializer.Serialize(MacroEntity);
            return xml.GetXmlNode(xd);
	    }

	    [Obsolete("This does nothing")]
        public void RefreshProperties()
        {
        }

		#region STATICS

		/// <summary>
		/// Creates a new macro given the name
		/// </summary>
		/// <param name="Name">Userfriendly name</param>
		/// <returns>The newly macro</returns>
		public static Macro MakeNew(string Name)
		{
		    var macro = new Umbraco.Core.Models.Macro
		        {
                    Name = Name,
                    Alias = Name.Replace(" ", String.Empty)
		        };

		    Current.Services.MacroService.Save(macro);

            var newMacro = new Macro(macro);

            return newMacro;
		}

		/// <summary>
		/// Retrieve all macroes
		/// </summary>
		/// <returns>A list of all macroes</returns>
		public static Macro[] GetAll()
		{
		    return Current.Services.MacroService.GetAll()
		                             .Select(x => new Macro(x))
		                             .ToArray();
		}

		/// <summary>
		/// Static contructor for retrieving a macro given an alias
		/// </summary>
        /// <param name="alias">The alias of the macro</param>
		/// <returns>If the macro with the given alias exists, it returns the macro, else null</returns>
        public static Macro GetByAlias(string alias)
		{
		    return Current.ApplicationCache.RuntimeCache.GetCacheItem<Macro>(
		        GetCacheKey(alias),
		        timeout:        TimeSpan.FromMinutes(30),
		        getCacheItem:   () =>
		            {
                        var macro = Current.Services.MacroService.GetByAlias(alias);
		                if (macro == null) return null;
		                return new Macro(macro);
		            });
		}

        public static Macro GetById(int id)
        {
            return Current.ApplicationCache.RuntimeCache.GetCacheItem<Macro>(
                GetCacheKey(string.Format("macro_via_id_{0}", id)),
                timeout:        TimeSpan.FromMinutes(30),
                getCacheItem:   () =>
                    {
                        var macro = Current.Services.MacroService.GetById(id);
                        if (macro == null) return null;
                        return new Macro(macro);
                    });
        }

        #region Macro Refactor

        private static string GetCacheKey(string alias)
        {
            return CacheKeys.MacroCacheKey + alias;
        }

        #endregion

		#endregion
	}
}
