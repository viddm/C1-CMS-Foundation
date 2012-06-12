﻿using System;
using System.Collections.Generic;
using System.Linq;
using Composite.Core.Collections.Generic;
using Composite.Core.Configuration;
using Composite.Core.Extensions;
using Composite.Core.PageTemplates.Plugins.Runtime;
using Composite.Core.PageTemplates.Foundation.PluginFacade;

namespace Composite.Core.PageTemplates.Foundation
{
    internal class PageTemplateProviderRegistryImpl : IPageTemplateProviderRegistry
    {
        private readonly ResourceLocker<Resources> _resourceLocker = new ResourceLocker<Resources>(new Resources(), Resources.DoInitialize);


        public void Flush()
        {
            _resourceLocker.ResetInitialization();
        }

        public IEnumerable<string> ProviderNames
        {
            get
            {
                return _resourceLocker.Resources.ProviderNames;
            }
        }

        private static IEnumerable<string> GetProviderNames()
        {
            var settings = ConfigurationServices.ConfigurationSource.GetSection(PageTemplateProviderSettings.SectionName)
                               as PageTemplateProviderSettings;

            return settings.PageTemplateProviders.Select(provider => provider.Name).ToArray();
        }


        public IEnumerable<PageTemplate> PageTemplates
        {
            get 
            {
                return _resourceLocker.Resources.PageTemplates;
            }
        }

        public IPageTemplateProvider GetProviderByTemplateId(Guid pageTemplateId)
        {
            return _resourceLocker.Resources.ProviderByTemplate[pageTemplateId];
        }



        private sealed class Resources
        {
            public IEnumerable<string> ProviderNames { get; set; }

            public IEnumerable<PageTemplate> PageTemplates { get; set; }
            public Hashtable<Guid, IPageTemplateProvider> ProviderByTemplate { get; set; }


            public static void DoInitialize(Resources resources)
            {
                var providerByTemplate = new Hashtable<Guid, IPageTemplateProvider>();
                resources.ProviderNames = GetProviderNames();

                var pageTemplates = new List<PageTemplate>();

                foreach (string providerName in resources.ProviderNames)
                {
                    var provider = PageTemplateProviderPluginFacade.GetProvider(providerName);
                    var templates = provider.GetPageTemplates();

                    pageTemplates.AddRange(templates);

                    foreach (var template in templates)
                    {
                        Verify.That(!providerByTemplate.ContainsKey(template.Id), 
                                    "There are muliple layouts with the same ID: '{0}'", template.Id);

                        providerByTemplate.Add(template.Id, provider);
                    }
                }

                resources.PageTemplates = pageTemplates;
                resources.ProviderByTemplate = providerByTemplate;
            }
        }
    }
}