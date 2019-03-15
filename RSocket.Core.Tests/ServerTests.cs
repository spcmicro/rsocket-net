using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RSocket.Transports;

namespace RSocket.Tests
{
	[TestClass]
	public class ServerTests
	{
		LoopbackTransport Loopback;
		RSocketClient Client;
		RSocketClient.ForStrings StringClient;
		RSocketServer Server;


		[TestMethod]
		public void ServerBasicTest()
		{
			Assert.AreNotEqual(Loopback.Input, Loopback.Beyond.Input, "Loopback Client/Server Inputs shouldn't be same.");
			Assert.AreNotEqual(Loopback.Output, Loopback.Beyond.Output, "Loopback Client/Server Outputs shouldn't be same.");
		}

		[TestMethod]
		public async Task ServerRequestResponseTest()
		{
			Server.Responder = async request => { await Task.CompletedTask; return (request.Data, request.Metadata); };
			var response = await StringClient.RequestResponse("TEST DATA", "METADATA?_____");
			Assert.AreEqual("TEST DATA", response, "Response should round trip.");
		}

		[TestMethod]
		public async Task ServerRequestStreamTest()
		{
            Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
                AsyncEnumerable.Range(0, 3)
                    .Select(i => (request.Data, request.Metadata));

			var (data, metadata) = ("TEST DATA", "METADATA?_____");
			var list = await StringClient.RequestStream(data, metadata).ToListAsync();
			Assert.AreEqual(3, list.Count, "Stream contents missing.");
			list.ForEach(item => Assert.AreEqual(item, data, "Stream contents mismatch."));
		}

		[TestMethod]
		public async Task ServerRequestStreamBinaryDetailsTest()
		{
			var count = 20;
            Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
                AsyncEnumerable.Range(0, count)
                    .Select(i => (
                        new ReadOnlySequence<byte>(request.Data.ToArray().Skip(i).Take(1).ToArray()),
                        new ReadOnlySequence<byte>(request.Metadata.ToArray().Skip(i).Take(1).ToArray())));

			var (requestData, requestMetadata) = (Enumerable.Range(1, count).Select(i => (byte)i).ToArray(), Enumerable.Range(100, count).Select(i => (byte)i).ToArray());
			var list = await Client.RequestStream(result => (Data: result.data.ToArray(), Metadata: result.metadata.ToArray()), new ReadOnlySequence<byte>(requestData), new ReadOnlySequence<byte>(requestMetadata)).ToListAsync();
			Assert.AreEqual(count, list.Count, "Stream contents missing.");

			for (int i = 0; i < list.Count; i++)
			{
				Assert.AreEqual(requestData[i], list[i].Data[0], "Data Sequence Mismatch");
				Assert.AreEqual(requestMetadata[i], list[i].Metadata[0], "Metadata Sequence Mismatch");
			}
		}


		//[TestMethod]
		//public async Task ServerRequestChannelTest()
		//{
		//	Server.Channeler = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request, IObservable<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> incoming) =>
		//	{
		//		Action<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> onNext = value => { };
		//		Action OnCompleted = () => { };
		//		var enumerable = new System.Collections.Async.AsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>(async yield =>
		//		{
		//			foreach (var index in Enumerable.Range(0, 3))
		//			{ await Task.CompletedTask; await yield.ReturnAsync((request.Data, request.Metadata)); }
		//		}).ToAsyncEnumerable();
		//		return (onNext, OnCompleted, enumerable);
		//	};

		//	var (data, metadata) = ("TEST DATA", "METADATA?_____");
		//	var list = await StringClient.RequestStream(data, metadata).ToListAsync();
		//	Assert.AreEqual(3, list.Count, "Stream contents missing.");
		//	list.ForEach(item => Assert.AreEqual(item, data, "Stream contents mismatch."));
		//}


		[TestInitialize]
		public void TestInitialize()
		{
			Loopback = new LoopbackTransport(DuplexPipe.ImmediateOptions, DuplexPipe.ImmediateOptions);
			Client = new RSocketClient(Loopback);
			Server = new RSocketServer(Loopback.Beyond);
			Client.ConnectAsync().Wait();
			Server.ConnectAsync().Wait();
			StringClient = new RSocketClient.ForStrings(Client);
		}
	}
}