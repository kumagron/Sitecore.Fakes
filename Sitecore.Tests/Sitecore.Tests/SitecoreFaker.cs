using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Data.Items;
using Sitecore.Collections;
using Sitecore.Data.Items.Fakes;
using Sitecore.Data.Fakes;
using Sitecore.Fakes;
using Sitecore.Data;
using Sitecore.Sites.Fakes;
using Sitecore.Globalization;
using Sitecore.Data.Fields.Fakes;
using Sitecore.Collections.Fakes;

namespace Sitecore.Tests
{
    public class SitecoreFaker
    {
        private readonly Language ContextLanguage = Language.Parse("en");
        public ShimItem Sitecore, Content, Website, Home;
        private Dictionary<ID, List<ShimField>> itemFields = new Dictionary<ID, List<ShimField>>();

        /// <summary>
        /// Throws an error when the calling method does not have the TestInitialize attribute.
        /// </summary>
        void EnforceTestInitialize()
        {
            var stackFrame = new StackFrame(2);
            var method = stackFrame.GetMethod();
            var hasInitAttrib = method.GetCustomAttributes(typeof(Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute), false).Any();

            if (!hasInitAttrib)
                throw new Exception("The calling method does not have the TestInitialize attribute.");
        }

        public void Initialize(Action<SitecoreFaker> onInitializing = null)
        {
            EnforceTestInitialize();

            Sitecore = CreateFakeItem(null, "sitecore", (sitecore) => {
                Content = CreateFakeItem(sitecore, "content", (content) =>
                {
                    Website = CreateFakeItem(content, "website", (site) =>
                    {
                        Home = CreateFakeItem(site, "home", (home) =>
                        {

                        });
                    });
                });
            });

            if (onInitializing != null)
                onInitializing(this);

            FakeBaseItem();
            FakeSitecoreContext();
        }

        public ShimItem CreateFakeItem(ShimItem parentItem, string name)
        {
            return CreateFakeItem(parentItem, name, (i, t, f) =>
            {
            });
        }

        public ShimItem CreateFakeItem(ShimItem parentItem, string name, Action<ShimItem> onItemCreating)
        {
            return CreateFakeItem(parentItem, name, (i, t, f) =>
            {
                if (onItemCreating != null)
                    onItemCreating(i);
            });
        }

        public ShimItem CreateFakeItem(ShimItem parentItem, string name, Action<ShimItem, List<ShimField>> onItemCreating)
        {
            return CreateFakeItem(parentItem, name, (i, f) =>
            {
                if (onItemCreating != null)
                    onItemCreating(i, f);
            });
        }

        public ShimItem CreateFakeItem(ShimItem parentItem, string name, Action<ShimItem, ShimTemplateItem> onItemCreating)
        {
            return CreateFakeItem(parentItem, name, (i, t, f) => {
                if (onItemCreating != null)
                    onItemCreating(i, t);
            });
        }

        public ShimItem CreateFakeItem(ShimItem parentItem, string name, Action<ShimItem, ShimTemplateItem, List<ShimField>> onItemCreating)
        {
            var id = ID.NewID;

            var item = new ShimItem()
            {
                IDGet = () => id,
                KeyGet = () => name.ToLower(),
                NameGet = () => name,
                HasChildrenGet = () => false,
                ParentGet = () => parentItem,
                PathsGet = () =>
                {
                    var path = (parentItem != null ? parentItem.Instance.Paths.Path : "") + "/" + name;

                    return new ShimItemPath()
                    {
                        PathGet = () => path,
                        FullPathGet = () => path,
                    };
                },
                LanguageGet = () => ContextLanguage,
                VersionsGet = () => new ShimItemVersions() { CountGet = () => { return 1; } }
            };

            //Bind item to parent item
            if (parentItem != null)
            {
                var children = parentItem.Instance.HasChildren ? parentItem.Instance.Children.ToList() : new List<Item>();
                children.Add(item);

                parentItem.HasChildrenGet = () => true;
                parentItem.ChildrenGet = () => new ChildList(parentItem.Instance, children);
                parentItem.GetChildren = () => parentItem.Instance.Children;
            }

            var templateItem = new ShimTemplateItem();
            var fields = new List<ShimField>();

            onItemCreating(item, templateItem, fields);

            item.TemplateGet = () => templateItem;
            item.FieldsGet = () => CreateFakeFieldCollection(item, fields);

            return item;
        }

        ShimFieldCollection CreateFakeFieldCollection(ShimItem item, List<ShimField> fields)
        {
            foreach (var field in fields)
                field.ItemGet = () => item;

            var fieldCollection = new ShimFieldCollection()
            {
                ItemGetString = (fieldName) =>
                {
                    return fields.SingleOrDefault(n => n.Instance.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                }
            };

            if (!itemFields.ContainsKey(item.Instance.ID))
                itemFields.Add(item.Instance.ID, fields);
            else
                itemFields[item.Instance.ID] = fields;

            fieldCollection.Bind(itemFields[item.Instance.ID]);

            return fieldCollection;
        }

        void FakeBaseItem()
        {
            ShimBaseItem.AllInstances.ItemGetString = (baseItem, fieldName) =>
            {
                Item result;

                TryGetItem(Sitecore.Instance.Children, (n) => object.Equals(baseItem, n), out result);

                if (result != null)
                {
                    var fields = itemFields[result.ID];

                    var field = fields.FirstOrDefault(n => n.Instance.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

                    if (field != null) return field.Instance.Value;
                }

                return string.Empty;
            };
        }

        void FakeSitecoreContext()
        {
            ShimContext.LanguageGet = () => ContextLanguage;
            ShimContext.SiteGet = () => new ShimSiteContext()
            {
                ContentLanguageGet = () => ContextLanguage
            };

            Func<Func<Item, bool>, Item> getItem = (predicate) =>
            {
                Item result;

                return TryGetItem(this.Sitecore.Instance.Children, predicate, out result) ? result : null;
            };

            ShimContext.DatabaseGet = () => new ShimDatabase()
            {
                GetItemString = (path) => getItem(n => n.Paths.Path.Equals(path, StringComparison.OrdinalIgnoreCase)),
                GetItemStringLanguage = (path, lang) => getItem(n => n.Paths.Path.Equals(path) && (n.Language.Equals(lang) || n.Languages != null && n.Languages.Any(l => l.Name.Equals(lang.Name)))),
                GetItemID = (id) => getItem(n => n.ID.Equals(id)),
                GetItemIDLanguage = (id, lang) => getItem(n => n.ID.Equals(id) && (n.Language.Equals(lang) || n.Languages != null && n.Languages.Any(l => l.Name.Equals(lang.Name)))),
            };
        }

        bool TryGetItem(ChildList children, Func<Item, bool> predicate, out Item result)
        {
            result = null;

            if (children == null || !children.Any()) return false;

            result = children.FirstOrDefault(predicate);

            if (result != null) return true;

            var query = children.Where(n => n.HasChildren);

            if (!query.Any()) return false;

            foreach (var child in query.ToArray())
            {
                if (TryGetItem(child.Children, predicate, out result))
                    return true;
            }

            return false;
        }
    }
}