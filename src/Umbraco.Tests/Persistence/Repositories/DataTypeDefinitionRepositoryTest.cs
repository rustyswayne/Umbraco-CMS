﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Tests.TestHelpers;
using Umbraco.Core.DI;

namespace Umbraco.Tests.Persistence.Repositories
{
    [TestFixture]
    [UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
    public class DataTypeDefinitionRepositoryTest : TestWithDatabaseBase
    {
        //protected override CacheHelper CreateCacheHelper()
        //{
        //    // hackish, but it works
        //    var testName = TestContext.CurrentContext.Test.Name;
        //    if (testName == "Can_Get_Pre_Value_As_String_With_Cache"
        //        || testName == "Can_Get_Pre_Value_Collection_With_Cache")
        //    {
        //        return new CacheHelper(
        //            new ObjectCacheRuntimeCacheProvider(),
        //            new StaticCacheProvider(),
        //            new StaticCacheProvider(),
        //            new IsolatedRuntimeCache(type => new ObjectCacheRuntimeCacheProvider())); // default would be NullCacheProvider
        //    }

        //    return base.CreateCacheHelper();
        //}

        protected override void ComposeCacheHelper()
        {
            // hackish, but it works
            var testName = TestContext.CurrentContext.Test.Name;
            if (testName == "Can_Get_Pre_Value_As_String_With_Cache" || testName == "Can_Get_Pre_Value_Collection_With_Cache")
            {
                var cacheHelper = new CacheHelper(
                    new ObjectCacheRuntimeCacheProvider(),
                    new StaticCacheProvider(),
                    new StaticCacheProvider(),
                    new IsolatedRuntimeCache(type => new ObjectCacheRuntimeCacheProvider())); // default would be NullCacheProvider

                Container.RegisterSingleton(f => cacheHelper);
                Container.RegisterSingleton(f => f.GetInstance<CacheHelper>().RuntimeCache);
            }
            else
            {
                base.ComposeCacheHelper();
            }
        }

        private IDataTypeDefinitionRepository CreateRepository(IDatabaseUnitOfWork unitOfWork)
        {
            return Container.GetInstance<IDatabaseUnitOfWork, IDataTypeDefinitionRepository>(unitOfWork);
        }

        private EntityContainerRepository CreateContainerRepository(IDatabaseUnitOfWork unitOfWork)
        {
            return new EntityContainerRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Logger, QueryFactory, Constants.ObjectTypes.DataTypeContainerGuid);
        }

        [TestCase("UmbracoPreVal87-21,3,48", 3, true)]
        [TestCase("UmbracoPreVal87-21,33,48", 3, false)]
        [TestCase("UmbracoPreVal87-21,33,48", 33, true)]
        [TestCase("UmbracoPreVal87-21,3,48", 33, false)]
        [TestCase("UmbracoPreVal87-21,3,48", 21, true)]
        [TestCase("UmbracoPreVal87-21,3,48", 48, true)]
        [TestCase("UmbracoPreVal87-22,33,48", 2, false)]
        [TestCase("UmbracoPreVal87-22,33,48", 22, true)]
        [TestCase("UmbracoPreVal87-22,33,44", 4, false)]
        [TestCase("UmbracoPreVal87-22,33,44", 44, true)]
        [TestCase("UmbracoPreVal87-22,333,44", 33, false)]
        [TestCase("UmbracoPreVal87-22,333,44", 333, true)]
        public void Pre_Value_Cache_Key_Tests(string cacheKey, int preValueId, bool outcome)
        {
            Assert.AreEqual(outcome, Regex.IsMatch(cacheKey, DataTypeDefinitionRepository.GetCacheKeyRegex(preValueId)));
        }

        [Test]
        public void Can_Move()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var containerRepository = CreateContainerRepository(unitOfWork);
                var repository = CreateRepository(unitOfWork);
                var container1 = new EntityContainer(Constants.ObjectTypes.DataTypeGuid) { Name = "blah1" };
                containerRepository.AddOrUpdate(container1);
                unitOfWork.Flush();

                var container2 = new EntityContainer(Constants.ObjectTypes.DataTypeGuid) { Name = "blah2", ParentId = container1.Id };
                containerRepository.AddOrUpdate(container2);
                unitOfWork.Flush();

                var dataType = (IDataTypeDefinition)new DataTypeDefinition(container2.Id, Constants.PropertyEditors.RadioButtonListAlias)
                {
                    Name = "dt1"
                };
                repository.AddOrUpdate(dataType);
                unitOfWork.Flush();

                //create a
                var dataType2 = (IDataTypeDefinition)new DataTypeDefinition(dataType.Id, Constants.PropertyEditors.RadioButtonListAlias)
                {
                    Name = "dt2"
                };
                repository.AddOrUpdate(dataType2);
                unitOfWork.Flush();

                var result = repository.Move(dataType, container1).ToArray();
                unitOfWork.Flush();

                Assert.AreEqual(2, result.Count());

                //re-get
                dataType = repository.Get(dataType.Id);
                dataType2 = repository.Get(dataType2.Id);

                Assert.AreEqual(container1.Id, dataType.ParentId);
                Assert.AreNotEqual(result.Single(x => x.Entity.Id == dataType.Id).OriginalPath, dataType.Path);
                Assert.AreNotEqual(result.Single(x => x.Entity.Id == dataType2.Id).OriginalPath, dataType2.Path);
            }

        }

        [Test]
        public void Can_Create_Container()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var containerRepository = CreateContainerRepository(unitOfWork);
                var container = new EntityContainer(Constants.ObjectTypes.DataTypeGuid) { Name = "blah" };
                containerRepository.AddOrUpdate(container);
                unitOfWork.Flush();
                Assert.That(container.Id, Is.GreaterThan(0));

                var found = containerRepository.Get(container.Id);
                Assert.IsNotNull(found);
            }
        }

        [Test]
        public void Can_Delete_Container()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var containerRepository = CreateContainerRepository(unitOfWork);
                var container = new EntityContainer(Constants.ObjectTypes.DataTypeGuid) { Name = "blah" };
                containerRepository.AddOrUpdate(container);
                unitOfWork.Flush();

                // Act
                containerRepository.Delete(container);
                unitOfWork.Flush();

                var found = containerRepository.Get(container.Id);
                Assert.IsNull(found);
            }
        }

        [Test]
        public void Can_Create_Container_Containing_Data_Types()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var containerRepository = CreateContainerRepository(unitOfWork);
                var repository = CreateRepository(unitOfWork);
                var container = new EntityContainer(Constants.ObjectTypes.DataTypeGuid) { Name = "blah" };
                containerRepository.AddOrUpdate(container);
                unitOfWork.Flush();

                var dataTypeDefinition = new DataTypeDefinition(container.Id, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.AddOrUpdate(dataTypeDefinition);
                unitOfWork.Flush();

                Assert.AreEqual(container.Id, dataTypeDefinition.ParentId);
            }
        }

        [Test]
        public void Can_Delete_Container_Containing_Data_Types()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var containerRepository = CreateContainerRepository(unitOfWork);
                var repository = CreateRepository(unitOfWork);
                var container = new EntityContainer(Constants.ObjectTypes.DataTypeGuid) { Name = "blah" };
                containerRepository.AddOrUpdate(container);
                unitOfWork.Flush();

                IDataTypeDefinition dataType = new DataTypeDefinition(container.Id, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.AddOrUpdate(dataType);
                unitOfWork.Flush();

                // Act
                containerRepository.Delete(container);
                unitOfWork.Flush();

                var found = containerRepository.Get(container.Id);
                Assert.IsNull(found);

                dataType = repository.Get(dataType.Id);
                Assert.IsNotNull(dataType);
                Assert.AreEqual(-1, dataType.ParentId);
            }
        }

        [Test]
        public void Can_Create()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);
                IDataTypeDefinition dataTypeDefinition = new DataTypeDefinition(-1, Constants.PropertyEditors.RadioButtonListAlias) {Name = "test"};

                repository.AddOrUpdate(dataTypeDefinition);

                unitOfWork.Flush();
                var id = dataTypeDefinition.Id;
                Assert.That(id, Is.GreaterThan(0));

                // Act
                dataTypeDefinition = repository.Get(id);

                // Assert
                Assert.That(dataTypeDefinition, Is.Not.Null);
                Assert.That(dataTypeDefinition.HasIdentity, Is.True);
            }
        }


        [Test]
        public void Can_Perform_Get_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);
                // Act
                var dataTypeDefinition = repository.Get(-42);

                // Assert
                Assert.That(dataTypeDefinition, Is.Not.Null);
                Assert.That(dataTypeDefinition.HasIdentity, Is.True);
                Assert.That(dataTypeDefinition.Name, Is.EqualTo("Dropdown"));
            }
        }

        [Test]
        public void Can_Perform_GetAll_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);

                // Act
                var dataTypeDefinitions = repository.GetAll();

                // Assert
                Assert.That(dataTypeDefinitions, Is.Not.Null);
                Assert.That(dataTypeDefinitions.Any(), Is.True);
                Assert.That(dataTypeDefinitions.Any(x => x == null), Is.False);
                Assert.That(dataTypeDefinitions.Count(), Is.EqualTo(24));
            }
        }

        [Test]
        public void Can_Perform_GetAll_With_Params_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);

                // Act
                var dataTypeDefinitions = repository.GetAll(-40, -41, -42);

                // Assert
                Assert.That(dataTypeDefinitions, Is.Not.Null);
                Assert.That(dataTypeDefinitions.Any(), Is.True);
                Assert.That(dataTypeDefinitions.Any(x => x == null), Is.False);
                Assert.That(dataTypeDefinitions.Count(), Is.EqualTo(3));
            }
        }

        [Test]
        public void Can_Perform_GetByQuery_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);

                // Act
                var query = QueryFactory.Create<IDataTypeDefinition>().Where(x => x.PropertyEditorAlias == Constants.PropertyEditors.RadioButtonListAlias);
                var result = repository.GetByQuery(query);

                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Any(), Is.True);
                Assert.That(result.FirstOrDefault().Name, Is.EqualTo("Radiobox"));
            }
        }

        [Test]
        public void Can_Perform_Count_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);

                // Act
                var query = QueryFactory.Create<IDataTypeDefinition>().Where(x => x.Name.StartsWith("D"));
                int count = repository.Count(query);

                // Assert
                Assert.That(count, Is.EqualTo(4));
            }
        }

        [Test]
        public void Can_Perform_Add_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);
                var dataTypeDefinition = new DataTypeDefinition("Test.TestEditor")
                {
                    DatabaseType = DataTypeDatabaseType.Integer,
                    Name = "AgeDataType",
                    CreatorId = 0
                };

                // Act
                repository.AddOrUpdate(dataTypeDefinition);
                unitOfWork.Flush();
                var exists = repository.Exists(dataTypeDefinition.Id);

                var fetched = repository.Get(dataTypeDefinition.Id);

                // Assert
                Assert.That(dataTypeDefinition.HasIdentity, Is.True);
                Assert.That(exists, Is.True);

                TestHelper.AssertAllPropertyValuesAreEquals(dataTypeDefinition, fetched, "yyyy-MM-dd HH:mm:ss");
            }
        }

        [Test]
        public void Can_Perform_Update_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);
                var dataTypeDefinition = new DataTypeDefinition("Test.blah")
                {
                    DatabaseType = DataTypeDatabaseType.Integer,
                    Name = "AgeDataType",
                    CreatorId = 0
                };
                repository.AddOrUpdate(dataTypeDefinition);
                unitOfWork.Flush();

                // Act
                var definition = repository.Get(dataTypeDefinition.Id);
                definition.Name = "AgeDataType Updated";
                definition.PropertyEditorAlias = "Test.TestEditor"; //change
                repository.AddOrUpdate(definition);
                unitOfWork.Flush();

                var definitionUpdated = repository.Get(dataTypeDefinition.Id);

                // Assert
                Assert.That(definitionUpdated, Is.Not.Null);
                Assert.That(definitionUpdated.Name, Is.EqualTo("AgeDataType Updated"));
                Assert.That(definitionUpdated.PropertyEditorAlias, Is.EqualTo("Test.TestEditor"));
            }
        }

        [Test]
        public void Can_Perform_Delete_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);
                var dataTypeDefinition = new DataTypeDefinition("Test.TestEditor")
                {
                    DatabaseType = DataTypeDatabaseType.Integer,
                    Name = "AgeDataType",
                    CreatorId = 0
                };

                // Act
                repository.AddOrUpdate(dataTypeDefinition);
                unitOfWork.Flush();
                var existsBefore = repository.Exists(dataTypeDefinition.Id);

                repository.Delete(dataTypeDefinition);
                unitOfWork.Flush();

                var existsAfter = repository.Exists(dataTypeDefinition.Id);

                // Assert
                Assert.That(existsBefore, Is.True);
                Assert.That(existsAfter, Is.False);
            }
        }

        [Test]
        public void Can_Perform_Exists_On_DataTypeDefinitionRepository()
        {
            // Arrange
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);

                // Act
                var exists = repository.Exists(1034); //Content picker
                var doesntExist = repository.Exists(-80);

                // Assert
                Assert.That(exists, Is.True);
                Assert.That(doesntExist, Is.False);
            }
        }

        [Test]
        public void Can_Get_Pre_Value_Collection()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);
                var dataTypeDefinition = new DataTypeDefinition(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.AddOrUpdate(dataTypeDefinition);
                unitOfWork.Flush();
                var dtid = dataTypeDefinition.Id;

                unitOfWork.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 0, Value = "test1"});
                unitOfWork.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 1, Value = "test2" });

                var collection = repository.GetPreValuesCollectionByDataTypeId(dtid);
                Assert.AreEqual(2, collection.PreValuesAsArray.Count());
            }
        }

        [Test]
        public void Can_Get_Pre_Value_As_String()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = CreateRepository(unitOfWork);
                var dataTypeDefinition = new DataTypeDefinition(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.AddOrUpdate(dataTypeDefinition);
                unitOfWork.Flush();
                var dtid = dataTypeDefinition.Id;

                var id = unitOfWork.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 0, Value = "test1" });
                unitOfWork.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 1, Value = "test2" });

                var val = repository.GetPreValueAsString(Convert.ToInt32(id));
                Assert.AreEqual("test1", val);
            }
        }

        [Test]
        public void Can_Get_Pre_Value_Collection_With_Cache()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            DataTypeDefinition dtd;
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = Container.GetInstance<IDatabaseUnitOfWork, IDataTypeDefinitionRepository>(unitOfWork);
                    dtd = new DataTypeDefinition(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.AddOrUpdate(dtd);
                unitOfWork.Flush();

                unitOfWork.Database.Insert(new DataTypePreValueDto() { DataTypeNodeId = dtd.Id, SortOrder = 0, Value = "test1" });
                unitOfWork.Database.Insert(new DataTypePreValueDto() { DataTypeNodeId = dtd.Id, SortOrder = 1, Value = "test2" });

                //this will cache the result
                var collection = repository.GetPreValuesCollectionByDataTypeId(dtd.Id);
            }

            // note: see CreateCacheHelper, this test uses a special cache
            var cache = CacheHelper.IsolatedRuntimeCache.GetCache<IDataTypeDefinition>();
            Assert.IsTrue(cache);
            var cached = cache.Result
                .GetCacheItemsByKeySearch<PreValueCollection>(CacheKeys.DataTypePreValuesCacheKey + dtd.Id + "-");

            Assert.IsNotNull(cached);
            Assert.AreEqual(1, cached.Count());
            Assert.AreEqual(2, cached.Single().FormatAsDictionary().Count);
        }

        [Test]
        public void Can_Get_Pre_Value_As_String_With_Cache()
        {
            var provider = TestObjects.GetDatabaseUnitOfWorkProvider(Logger);
            DataTypeDefinition dtd;
            object id;
            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = Container.GetInstance<IDatabaseUnitOfWork, IDataTypeDefinitionRepository>(unitOfWork);
                dtd = new DataTypeDefinition(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.AddOrUpdate(dtd);
                unitOfWork.Flush();

                id = unitOfWork.Database.Insert(new DataTypePreValueDto() { DataTypeNodeId = dtd.Id, SortOrder = 0, Value = "test1" });
                unitOfWork.Database.Insert(new DataTypePreValueDto() { DataTypeNodeId = dtd.Id, SortOrder = 1, Value = "test2" });

                //this will cache the result
                var val = repository.GetPreValueAsString(Convert.ToInt32(id));
            }

            // note: see CreateCacheHelper, this test uses a special cache
            var cache = CacheHelper.IsolatedRuntimeCache.GetCache<IDataTypeDefinition>();
            Assert.IsTrue(cache);
            var cached = cache.Result
                .GetCacheItemsByKeySearch<PreValueCollection>(CacheKeys.DataTypePreValuesCacheKey + dtd.Id + "-");

            Assert.IsNotNull(cached);
            Assert.AreEqual(1, cached.Count());
            Assert.AreEqual(2, cached.Single().FormatAsDictionary().Count);

            using (var unitOfWork = provider.CreateUnitOfWork())
            {
                var repository = Container.GetInstance<IDatabaseUnitOfWork, IDataTypeDefinitionRepository>(unitOfWork);
                //ensure it still gets resolved!
                var val = repository.GetPreValueAsString(Convert.ToInt32(id));
                Assert.AreEqual("test1", val);
            }
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }
    }
}