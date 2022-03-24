using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;
using Shane32.GraphQL.AspNetCore;

namespace Tests
{
    public class UserContextBuilderTests
    {
        [Fact]
        public void NullChecks()
        {
            Func<HttpContext, MyUserContext> func = null!;
            Should.Throw<ArgumentNullException>(() => new UserContextBuilder<MyUserContext>(func));
            Func<HttpContext, ValueTask<MyUserContext>> func2 = null!;
            Should.Throw<ArgumentNullException>(() => new UserContextBuilder<MyUserContext>(func2));
        }

        [Fact]
        public async Task Sync_Works()
        {
            var context = Mock.Of<HttpContext>(MockBehavior.Strict);
            var userContext = new MyUserContext();
            var builder = new UserContextBuilder<MyUserContext>(context2 => {
                context2.ShouldBe(context);
                return userContext;
            });
            (await builder.BuildUserContextAsync(context)).ShouldBe(userContext);
        }

        [Fact]
        public async Task Async_Works()
        {
            var context = Mock.Of<HttpContext>(MockBehavior.Strict);
            var userContext = new MyUserContext();
            var builder = new UserContextBuilder<MyUserContext>(context2 => {
                context2.ShouldBe(context);
                return new ValueTask<MyUserContext>(userContext);
            });
            (await builder.BuildUserContextAsync(context)).ShouldBe(userContext);
        }

        private class MyUserContext : Dictionary<string, object?> { }
    }
}
