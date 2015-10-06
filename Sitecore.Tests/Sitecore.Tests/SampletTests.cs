using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sitecore.Tests
{
    [TestClass]
    public class SampletTests
    {
        IDisposable _context;
        readonly SitecoreFaker _scFaker = new SitecoreFaker();

        [TestInitialize]
        public void Initialize()
        {
            _context = ShimsContext.Create();
            _scFaker.Initialize();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Dispose();
        }

        [TestMethod]
        public void Sample_Test()
        {
            // Arrange:
            var expectedItem = _scFaker.CreateFakeItem(_scFaker.Home, "sample item");

            // Act:
            var actualItem = Context.Database.GetItem(expectedItem.Instance.Paths.FullPath);

            // Assert:
            Assert.AreEqual(expectedItem, actualItem);
        }

    }
}
