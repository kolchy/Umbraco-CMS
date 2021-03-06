﻿using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using AutoMapper;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Mvc;
using umbraco;
using Umbraco.Web.WebApi;
using System;
using System.Net.Http.Headers;
using System.Web;
using System.IO;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using System.Linq;
using System.Text.RegularExpressions;

namespace Umbraco.Web
{
    internal static class TuningUtility
    {

        internal static string tuningStylePath = "/Css/tuning/";
        internal static string resultLessPath = tuningStylePath + @"{0}.less";
        internal static string resultCssPath = tuningStylePath + @"{0}.css";
        internal static string frontBasePath = HttpContext.Current.Server.MapPath(@"\Css\tuning\");

        // get style box by tag
        internal static String GetStyleBlock(string source, string name) 
        {

            string startTag = string.Format("/***start-{0}***/", name);
            string endTag = string.Format("/***end-{0}***/", name);

            int indexStartTag = source.IndexOf(startTag);
            int indexEndTag = source.IndexOf(endTag);

            if (indexStartTag >= 0 && indexEndTag >= 0)
                return source.Substring(indexStartTag, indexEndTag - indexStartTag).Replace(startTag, "").Replace(endTag, "");
            else
                return "";

        }

        // Get less parameters from lessPath file, both general parameters and grid parameters
        internal static string GetLessParameters(int pageId)
        {

            // Load current page
            var contentService = ApplicationContext.Current.Services.ContentService;
            IContent content = contentService.GetById(pageId);

            // Get less file path from the page Id
            string lessPath = GetStylesheetPath(content.Path.Split(','), true);

            string paramBlock = string.Empty;
            if (System.IO.File.Exists(HttpContext.Current.Server.MapPath(lessPath)))
            {
                using (System.IO.StreamReader sr = new System.IO.StreamReader(HttpContext.Current.Server.MapPath(lessPath)))
                {

                    string lessContent = sr.ReadToEnd();
                    foreach (Match match in Regex.Matches(lessContent, string.Format("@([^;\n]*):([^;\n]*);")))
                    {
                        paramBlock = paramBlock + match.Groups[0].Value + "\r\n";
                    }
                }
            }
            return paramBlock;

        }

        // Get inherited pageId with tuning
        internal static int GetParentOrSelfTunedPageId(string[] path, bool preview) 
        {

            string styleTuning = preview ? @"{0}{1}.less" : "{0}{1}.css";
            foreach (var page in path.OrderByDescending(r => path.IndexOf(r)))
            {
                string stylePath = HttpContext.Current.Server.MapPath(string.Format(styleTuning, tuningStylePath, page));
                if (System.IO.File.Exists(stylePath))
                {
                    return int.Parse(page);
                }
            }

            if (preview)
                return int.Parse(path[1]);
            else
                return -1;

        }

        // Get stylesheet path for current page
        internal static string GetStylesheetPath(string[] path, bool preview)
        {
            string styleTuning = preview ? @"{0}{1}.less" : "{0}{1}.css";

            int tunedPageId = GetParentOrSelfTunedPageId(path, preview);

            if (tunedPageId >0)
                return string.Format(styleTuning, tuningStylePath, tunedPageId);
            else
                return string.Empty;
        }

        // Create new less file
        internal static string CreateOrUpdateLessFile(int pageId, string configs)
        {

            // Load current page
            var contentService = ApplicationContext.Current.Services.ContentService;
            IContent content = contentService.GetById(pageId);

            // Get less file path from the page Id
            string lessPath = GetStylesheetPath(content.Path.Split(','), true);

            // If less file exist, Load its  content
            string lessContent = string.Empty;
            if (System.IO.File.Exists(HttpContext.Current.Server.MapPath(lessPath)))
            {
                using (System.IO.StreamReader sr = new System.IO.StreamReader(HttpContext.Current.Server.MapPath(lessPath)))
                {
                    lessContent = sr.ReadToEnd();
                }
            }

            // Parse the config file
            // for each config read and add less script in global less
            dynamic tuningConfigs = Newtonsoft.Json.Linq.JObject.Parse(configs.ToString());
            foreach (var configuration in tuningConfigs.configs)
            {
                foreach (var editorItem in configuration.editors)
                {

                    var type = (editorItem.type != null && !string.IsNullOrEmpty(editorItem.type.ToString())) ? editorItem.type.ToString() : string.Empty;
                    var alias = (editorItem.alias != null && !string.IsNullOrEmpty(editorItem.alias.ToString())) ? editorItem.alias.ToString() : string.Empty;
                    var schema = (configuration.schema != null && !string.IsNullOrEmpty(configuration.schema.ToString())) ? configuration.schema.ToString() : alias;
                    schema = (editorItem.schema != null && !string.IsNullOrEmpty(editorItem.schema.ToString())) ? editorItem.schema.ToString() : schema;

                    if (string.IsNullOrEmpty(GetStyleBlock(lessContent, "lessParam-" + alias)))
                    {

                        // read the less model file
                        var lessModelPath = string.Format("/Umbraco/assets/less/{0}.less", type);
                        var lessModel = string.Empty;
                        if (System.IO.File.Exists(HttpContext.Current.Server.MapPath(lessModelPath)))
                        {
                            using (System.IO.StreamReader sr = new System.IO.StreamReader(HttpContext.Current.Server.MapPath(lessModelPath)))
                            {
                                lessModel = sr.ReadToEnd();
                            }
                        }

                        lessContent = lessContent + Environment.NewLine + lessModel
                            .Replace("-ALIAS-", alias.ToLower())
                            .Replace("-SCHEMA-", schema);

                        foreach (var parameter in editorItem)
                        {
                            lessContent = lessContent.Replace("-" + parameter.Name.ToString().ToUpper() + "-", parameter.Value.ToString());
                        }

                    }
                }
            }

            //// Create front directory if necesary
            if (!Directory.Exists(frontBasePath))
                Directory.CreateDirectory(frontBasePath);

            // Save less file
            if (string.IsNullOrEmpty(lessPath)) lessPath = string.Format("{0}{1}.less", tuningStylePath, pageId);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(HttpContext.Current.Server.MapPath(lessPath)))
            {
                file.Write(lessContent);
            }




            //string lessPath = GetStylesheetPath(path, true);

            //// If less file exist, Load its  content
            //string lessContent = string.Empty;
            //using (System.IO.StreamReader sr = new System.IO.StreamReader(System.IO.File.Exists(HttpContext.Current.Server.MapPath(lessPath)) 
            //    ? HttpContext.Current.Server.MapPath(lessPath) : tuningDefaultLessPath))
            //{
            //    lessContent = sr.ReadToEnd();
            //}

            //// Update with grid row style needs
            //string[] gridRows = TuningUtility.GetGridRows(pageId);
            //string parametersToAdd = string.Empty;
            //string styleToAdd = string.Empty;
            //foreach (var gridRow in gridRows)
            //{
            //    if (string.IsNullOrEmpty(GetStyleBlock(lessContent, "gridStyle-" + "gridrow_" + gridRow))) 
            //    {
            //        parametersToAdd += NewGridRowBlock(gridRow);
            //    }
            //}

            //// Create front directory if necesary
            //if (!Directory.Exists(frontBasePath))
            //    Directory.CreateDirectory(frontBasePath);

            //// Save less file
            //if (string.IsNullOrEmpty(lessPath)) lessPath = string.Format("{0}{1}.less", tuningStylePath, pageId);
            //lessContent = lessContent + Environment.NewLine + parametersToAdd;
            //using (System.IO.StreamWriter file = new System.IO.StreamWriter(HttpContext.Current.Server.MapPath(lessPath)))
            //{
            //    file.Write(lessContent);
            //}

            return lessPath;

        }

        // Save and publish less style
        internal static void SaveAndPublishStyle(string parameters, int pageId, bool inherited) 
        {

            // Get inherited tuned pageId and path
            var contentService = ApplicationContext.Current.Services.ContentService;
            IContent content = contentService.GetById(pageId);
            int inheritedTunedPageId = TuningUtility.GetParentOrSelfTunedPageId(content.Path.Split(','), true);

            // Load inherited Less content
            string inheritedLessContent = string.Empty;
            using (System.IO.StreamReader sr = new System.IO.StreamReader(HttpContext.Current.Server.MapPath(string.Format(resultLessPath, inheritedTunedPageId))))
            {
                inheritedLessContent = sr.ReadToEnd();
            }

            // Update pageId if parameters have changed
            if (inherited) pageId = inheritedTunedPageId;
            
            // Create front directory if necesary
            if (!Directory.Exists(frontBasePath))
                Directory.CreateDirectory(frontBasePath);

            // Prepare parameters and gf block and replace with the new value
            string newParamBlock = string.Empty;
            string newGfBlock = string.Empty;
            foreach (string parameter in parameters.Trim().Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (parameter.IndexOf("@import") < 0)
                {
                    string name = parameter.Substring(0, parameter.IndexOf(":"));
                    string value = parameter.Substring(parameter.IndexOf(":") + 1);
                    if (string.IsNullOrEmpty(value)) value = "''";
                    inheritedLessContent = Regex.Replace(inheritedLessContent, string.Format("{0}:([^;\n]*);", name), string.Format("{0}:{1};", name, value));
                }
                else
                {
                    newGfBlock += parameter + ";" + Environment.NewLine;
                }
            }

            // Save less file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(HttpContext.Current.Server.MapPath(string.Format(resultLessPath, pageId))))
            {
                file.Write(inheritedLessContent);
            }

            // Compile the Less file
            string compiledStyle = GetCssFromLessString(newGfBlock + inheritedLessContent, false, true, true).Replace("@import", "@IMPORT");

            // Save compiled file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(HttpContext.Current.Server.MapPath(string.Format(resultCssPath, pageId))))
            {
                file.Write(compiledStyle);
            }

        }

        // Delete tuning style
        internal static void DeleteStyle(int pageId)
        {

            // Path to the less and css files
            string newResultLessPath = HttpContext.Current.Server.MapPath(string.Format(resultLessPath, pageId));
            string newResultCssPath = HttpContext.Current.Server.MapPath(string.Format(resultCssPath, pageId));

            // Delete all style file for this page
            System.IO.File.Delete(newResultLessPath);
            System.IO.File.Delete(newResultCssPath);

        }

        // Compile and compress less style
        private static string GetCssFromLessString(string css, Boolean cacheEnabled, Boolean minifyOutput, Boolean disableVariableRedefines)
        {
            var config = dotless.Core.configuration.DotlessConfiguration.GetDefaultWeb();
            config.DisableVariableRedefines = disableVariableRedefines;
            config.CacheEnabled = cacheEnabled;
            config.MinifyOutput = minifyOutput;
            return dotless.Core.LessWeb.Parse(css, config);
        }

    }
}
