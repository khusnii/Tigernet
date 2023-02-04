﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Tigernet.Hosting.Actions;
using Tigernet.Hosting.Attributes;

namespace Tigernet.Hosting
{
    public class TigernetHostBuilder
    {
        private readonly string _prefix;
        private readonly HttpListener _listener = new HttpListener();
        private readonly Dictionary<string, Func<HttpListenerContext, Task>> _routes = new Dictionary<string, Func<HttpListenerContext, Task>>();

        public TigernetHostBuilder(string prefix)
        {
            _prefix = prefix;
            _listener.Prefixes.Add(prefix);
        }


        public void MapRoute(string route, Func<HttpListenerContext, Task> handler)
        {
            _routes.Add(route, handler);
        }

        public async Task Start()
        {
            _listener.Start();
            Console.WriteLine("Server is running on " + _prefix);
            while (true)
            {
                var context = await _listener.GetContextAsync();
                await HandleRequest(context);
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            Func<HttpListenerContext, Task> handler;
            if (_routes.TryGetValue(request.RawUrl, out handler))
            {
                await handler(context);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
            }
        }

        public void MapRester<T>(string route) where T : ResterBase, new()
        {
            var rester = new T();
            var type = rester.GetType();
            var methods = type.GetMethods();
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(GetterAttribute), false);
                if (attributes.Length > 0)
                {
                    var attribute = attributes[0] as GetterAttribute;
           
                    var routeUrl = route + attribute.Route;
                    MapRoute(routeUrl, async context =>
                    {
                        var response = context.Response;
                        response.ContentType = "application/json";
                        var result = method.Invoke(rester, null);
                        var content = Encoding.UTF8.GetBytes(result.ToString());
                        response.ContentLength64 = content.Length;
                        using (var output = response.OutputStream)
                        {
                            await output.WriteAsync(content, 0, content.Length);
                        }
                    });
                }
            }
        }
    }
}