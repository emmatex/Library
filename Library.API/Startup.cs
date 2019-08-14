using AspNetCoreRateLimit;
using Library.API.Contexts;
using Library.API.Entities;
using Library.API.Helpers;
using Library.API.Models;
using Library.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using System.Linq;

namespace Library.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(options => {
                // Return a 406 when an unsupported media type was requested
                options.ReturnHttpNotAcceptable = true;

                // Add XML formatters
                options.OutputFormatters.Add(new XmlDataContractSerializerOutputFormatter());
                // options.InputFormatters.Add(new XmlDataContractSerializerInputFormatter(options));
                var xmlDataContractSerializerInputFormatter =  new XmlDataContractSerializerInputFormatter();
                xmlDataContractSerializerInputFormatter.SupportedMediaTypes.Add("application/vnd.ifeanyi.authorwithdateofdeath.full+xml");
                options.InputFormatters.Add(xmlDataContractSerializerInputFormatter);

                var jsonInputFormatter = options.InputFormatters.OfType<JsonInputFormatter>().FirstOrDefault();
                if (jsonInputFormatter != null)
                {
                    jsonInputFormatter.SupportedMediaTypes.Add("application/vnd.ifeanyi.author.full+json");
                    jsonInputFormatter.SupportedMediaTypes.Add("application/vnd.ifeanyi.authorwithdateofdeath.full+json");
                }
                var jsonOutputFormatter = options.OutputFormatters.OfType<JsonOutputFormatter>().FirstOrDefault();
                if (jsonOutputFormatter != null)
                {
                    jsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.ifeanyi.hateoas+json");
                }
            })
            .AddJsonOptions(options =>
             {
                 options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
             });

            var connectionString = Configuration["connectionStrings:libraryDBConnectionString"];
            services.AddDbContext<LibraryContext>(o => o.UseSqlServer(connectionString));

            // register the repository
            services.AddScoped<ILibraryRepository, LibraryRepository>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddScoped<IUrlHelper>(implementationFactory => {
                var actionContext = implementationFactory.GetService<IActionContextAccessor>().ActionContext;
                return new UrlHelper(actionContext);
            });
            services.AddTransient<IPropertyMappingService, PropertyMappingService>();
            services.AddTransient<ITypeHelperService, TypeHelperService>();

            // Caching middleware
            services.AddHttpCacheHeaders((expirationModelOptions) => { expirationModelOptions.MaxAge = 600; },
                (validationModelOptions) => { validationModelOptions.AddMustRevalidate = true;  });

            //services.AddResponseCaching();
            services.AddMemoryCache();

            //Rate limit middleware
            services.Configure<IpRateLimitOptions>((options) =>
            {
                options.GeneralRules = new System.Collections.Generic.List<RateLimitRule>()
                {
                    new RateLimitRule()
                    {
                        Endpoint = "*",
                        Limit = 1000,
                        Period = "5m"
                    },
                    new RateLimitRule()
                    {
                        Endpoint = "*",
                        Limit = 200,
                        Period = "10s"
                    }
                };
            });

            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, 
            ILoggerFactory loggerFactory, LibraryContext libraryContext)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(appBuilder =>
                {
                    appBuilder.Run(async context => {
                        var exceptionHandleFeature = context.Features.Get<IExceptionHandlerFeature>();
                        if(exceptionHandleFeature != null)
                        {
                            var logger = loggerFactory.CreateLogger("Globle exception logger");
                            logger.LogError(500, exceptionHandleFeature.Error, exceptionHandleFeature.Error.Message);
                        }

                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("An unexpected fault happened. Try again later.");
                    });
                });
            }

            AutoMapper.Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<Author, AuthorDto>().ForMember(dest => dest.Name, opt => opt.MapFrom(src =>
                $"{src.FirstName} {src.LastName}")).ForMember(dest => dest.Age, opt => opt.MapFrom(src =>
                   src.DateOfBirth.GetCurrentAge(src.DateOfDeath)));

                cfg.CreateMap<Book, BookDto>();
                cfg.CreateMap<AuthorForCreateDto, Author>();
                cfg.CreateMap<AuthorForCreationWithDateOfDeathDto, Author>();
                cfg.CreateMap<BookForCreateDto, Book>();
                cfg.CreateMap<BookForUpdateDto, Book>();
                cfg.CreateMap<Book, BookForUpdateDto>();
            });

            libraryContext.EnsureSeedDataForContext();
            app.UseIpRateLimiting();
           // app.UseResponseCaching();
            app.UseHttpCacheHeaders();
            app.UseMvc();
        }
    }
}
