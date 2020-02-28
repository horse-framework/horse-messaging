using Test.SocketModels.Helpers;
using Test.SocketModels.Models;
using Twino.SerializableModel.Serialization;
using Xunit;

namespace Test.SocketModels
{
    public class CustomModelRW
    {
        private readonly IModelWriter _writer;
        private readonly IModelReader _reader;

        public CustomModelRW()
        {
            _writer = new CustomModelWriter();
            _reader = new CustomModelReader();
        }

        [Fact]
        public void Write()
        {
            DefaultModel model = new DefaultModel();
            model.Type = 123;
            model.Number = 500;
            model.Name = "Default";

            string serialized = _writer.Serialize(model);
            Assert.Equal("123={\"type\":123,\"name\":\"Default\",\"number\":500}", serialized);
        }

        [Fact]
        public void Read()
        {
            string serialized = "123={\"type\":123,\"name\":\"Default\",\"number\":500}";

            DefaultModel model = (DefaultModel)_reader.Read(typeof(DefaultModel), serialized);
            DefaultModel gmodel = _reader.Read<DefaultModel>(serialized);

            Assert.Equal(123, model.Type);
            Assert.Equal(123, gmodel.Type);

            Assert.Equal(500, model.Number);
            Assert.Equal(500, gmodel.Number);

            Assert.Equal("Default", model.Name);
            Assert.Equal("Default", gmodel.Name);
        }

        [Fact]
        public void WriteRead()
        {
            DefaultModel smodel = new DefaultModel();
            smodel.Type = 123;
            smodel.Number = 500;
            smodel.Name = "Default";

            string serialized = _writer.Serialize(smodel);

            DefaultModel model = (DefaultModel)_reader.Read(typeof(DefaultModel), serialized);
            DefaultModel gmodel = _reader.Read<DefaultModel>(serialized);

            Assert.Equal(123, model.Type);
            Assert.Equal(123, gmodel.Type);

            Assert.Equal(500, model.Number);
            Assert.Equal(500, gmodel.Number);

            Assert.Equal("Default", model.Name);
            Assert.Equal("Default", gmodel.Name);
        }
    }
}