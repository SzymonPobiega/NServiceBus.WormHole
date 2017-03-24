using System;
using System.Text;
using System.Threading.Tasks;

namespace ServerTest
{
    using Microsoft.Net.Http.Server;

    class Program
    {
        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var settings = new WebListenerSettings();
            settings.UrlPrefixes.Add("http://localhost:7777");

            using (var listener = new WebListener(settings))
            {
                listener.Start();

                while (true)
                {
                    var context = await listener.AcceptAsync();
                    var bytes = Encoding.ASCII.GetBytes("Hello World: " + DateTime.Now);
                    context.Response.ContentLength = bytes.Length;
                    context.Response.ContentType = "text/plain";

                    var bodyLenght = context.Request.Body.Length;

                    await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                    context.Dispose();
                }
            }
        }
    }
}
