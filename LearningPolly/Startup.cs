using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Registry;

namespace LearningPolly
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => Configuration = configuration;

        private IPolicyRegistry<string> _myRegistry;

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var circuitBreakerPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(2, TimeSpan.FromMinutes(2), OnBreak, OnReset, OnHalfOpen);

            _myRegistry = new PolicyRegistry();

            var httpClient = new HttpClient {BaseAddress = new Uri("http://localhost:5000/api/")};
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            services.AddSingleton(httpClient);
            services.AddSingleton(circuitBreakerPolicy);
            services.AddMvc();

            services.AddControllers();
            services.AddSwaggerGen(
                c => c.SwaggerDoc("v1", new OpenApiInfo {Title = "LearningPolly", Version = "v1"}));
        }

        private void OnHalfOpen() => Debug.WriteLine("Connection is half-open.");
        private void OnReset() => Debug.WriteLine("Connection is reset.");

        private void OnBreak(
                DelegateResult<HttpResponseMessage> delegateResult,
                TimeSpan timeSpan) =>
            Debug.WriteLine(
                $"Connection is broken: {delegateResult.Result}, {delegateResult.Result}");

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IMemoryCache memoryCache)
        {
            var memoryCacheProvider = new Polly.Caching.Memory.MemoryCacheProvider(memoryCache);
            var cachePolicy = Policy
                .CacheAsync<HttpResponseMessage>(memoryCacheProvider, TimeSpan.FromMinutes(5));

            _myRegistry.Add("myLocalCachePolicy", cachePolicy);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(
                    c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "LearningPolly v1"));
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
