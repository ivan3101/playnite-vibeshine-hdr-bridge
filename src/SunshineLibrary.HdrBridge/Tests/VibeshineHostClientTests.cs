using Microsoft.VisualStudio.TestTools.UnitTesting;
using SunshineLibrary.Models;
using SunshineLibrary.Services.Hosts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SunshineLibrary.Tests
{
    [TestClass]
    public class VibeshineHostClientTests
    {
        [TestMethod]
        public async Task ListApps_JoinsDuplicateNamesByStablePlayniteId()
        {
            var handler = new JsonHandler(new Dictionary<string, string>
            {
                ["api/apps"] = @"{""apps"":[
                    {""name"":""Same"",""uuid"":""app-a"",""playnite-id"":""game-a""},
                    {""name"":""Same"",""uuid"":""app-b"",""playnite-id"":""game-b""}
                ]}",
                ["api/playnite/games"] = @"[
                    {""id"":""game-a"",""name"":""Same"",""installed"":true,""categories"":[""HDR Bridge: HDR""]},
                    {""id"":""game-b"",""name"":""Same"",""installed"":true,""categories"":[]}
                ]",
            });

            using (var client = new VibeshineHostClient(Config(), handler))
            {
                var result = await client.ListAppsAsync(CancellationToken.None);
                Assert.IsTrue(result.IsOk);
                Assert.AreEqual(2, result.Value.Count);
                Assert.AreEqual("game-a", result.Value[0].PlayniteId);
                CollectionAssert.Contains(result.Value[0].Categories, "HDR Bridge: HDR");
                Assert.AreEqual("game-b", result.Value[1].PlayniteId);
                Assert.AreEqual(0, result.Value[1].Categories.Count);
            }
        }

        [TestMethod]
        public async Task ListApps_UsesUniqueNameFallbackForOlderPayload()
        {
            var handler = new JsonHandler(new Dictionary<string, string>
            {
                ["api/apps"] = @"{""apps"":[{""name"":""Game"",""uuid"":""app""}]}",
                ["api/playnite/games"] = @"[{""id"":""game-id"",""name"":""Game"",""installed"":true,""categories"":[]}]",
            });

            using (var client = new VibeshineHostClient(Config(), handler))
            {
                var result = await client.ListAppsAsync(CancellationToken.None);
                Assert.IsTrue(result.IsOk);
                Assert.AreEqual("game-id", result.Value.Single().PlayniteId);
            }
        }

        [TestMethod]
        public async Task ListApps_AmbiguousNameFallbackDoesNotAssignWrongIdentity()
        {
            var handler = new JsonHandler(new Dictionary<string, string>
            {
                ["api/apps"] = @"{""apps"":[{""name"":""Same"",""uuid"":""app""}]}",
                ["api/playnite/games"] = @"[
                    {""id"":""a"",""name"":""Same"",""installed"":true,""categories"":[]},
                    {""id"":""b"",""name"":""Same"",""installed"":true,""categories"":[]}
                ]",
            });

            using (var client = new VibeshineHostClient(Config(), handler))
            {
                var result = await client.ListAppsAsync(CancellationToken.None);
                Assert.IsTrue(result.IsOk);
                Assert.IsNull(result.Value.Single().PlayniteId);
                Assert.IsNull(result.Value.Single().Categories);
            }
        }

        [TestMethod]
        public async Task RequestsUseConfiguredBearerToken()
        {
            var handler = new JsonHandler(new Dictionary<string, string>
            {
                ["api/apps"] = @"{""apps"":[]}",
                ["api/playnite/games"] = @"[]",
            });

            using (var client = new VibeshineHostClient(Config(), handler))
            {
                var result = await client.ListAppsAsync(CancellationToken.None);
                Assert.IsTrue(result.IsOk);
            }

            Assert.IsTrue(handler.Authorization.All(value => value == "Bearer test-token"));
            Assert.AreEqual(2, handler.Authorization.Count);
        }

        [TestMethod]
        public async Task FailedPlayniteEndpoint_DoesNotReturnPartialSnapshot()
        {
            var handler = new JsonHandler(new Dictionary<string, string>
            {
                ["api/apps"] = @"{""apps"":[{""name"":""Game"",""uuid"":""app"",""playnite-id"":""game-id""}]}",
            });

            using (var client = new VibeshineHostClient(Config(), handler))
            {
                var result = await client.ListAppsAsync(CancellationToken.None);
                Assert.IsFalse(result.IsOk);
                Assert.AreEqual(HostResultKind.ServerError, result.Kind);
            }
        }

        [TestMethod]
        public async Task PasswordCredentials_UseBasicAuthorizationWithoutLegacyLogin()
        {
            var handler = new JsonHandler(new Dictionary<string, string>
            {
                ["api/apps"] = @"{""apps"":[]}",
                ["api/playnite/games"] = @"[]",
            });
            var config = Config();
            config.ApiToken = null;
            config.AdminUser = "admin";
            config.AdminPassword = "password";

            using (var client = new VibeshineHostClient(config, handler))
            {
                var result = await client.ListAppsAsync(CancellationToken.None);
                Assert.IsTrue(result.IsOk);
            }

            Assert.AreEqual(2, handler.Authorization.Count);
            Assert.IsTrue(handler.Authorization.All(value => value.StartsWith("Basic ")));
            Assert.IsFalse(handler.RequestPaths.Contains("api/login"));
        }

        [TestMethod]
        public void DuplicatePlayniteIds_AreExcludedFromStableLookup()
        {
            var lookups = VibeshineHostClient.BuildPlayniteLookups(new List<VibeshinePlayniteGameDto>
            {
                new VibeshinePlayniteGameDto { Id = "duplicate", Name = "First", Installed = true },
                new VibeshinePlayniteGameDto { Id = "duplicate", Name = "Second", Installed = true },
            });

            Assert.IsFalse(lookups.ById.ContainsKey("duplicate"));
        }

        private static HostConfig Config() => new HostConfig
        {
            Id = Guid.NewGuid(),
            Label = "Vibeshine",
            Address = "127.0.0.1",
            Port = 47990,
            ApiToken = "test-token",
            ServerType = ServerType.Vibeshine,
        };

        private sealed class JsonHandler : HttpMessageHandler
        {
            private readonly IReadOnlyDictionary<string, string> responses;
            public List<string> Authorization { get; } = new List<string>();
            public List<string> RequestPaths { get; } = new List<string>();

            public JsonHandler(IReadOnlyDictionary<string, string> responses)
            {
                this.responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Authorization.Add(request.Headers.Authorization?.ToString());
                var path = request.RequestUri.IsAbsoluteUri
                    ? request.RequestUri.PathAndQuery.TrimStart('/')
                    : request.RequestUri.OriginalString.TrimStart('/');
                RequestPaths.Add(path);
                if (!responses.TryGetValue(path, out var json))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                });
            }
        }
    }
}
