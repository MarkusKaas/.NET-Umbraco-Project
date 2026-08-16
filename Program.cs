WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MyCustomUmbracoProject.Services.ChatHistoryService>();
builder.Services.AddSingleton<MyCustomUmbracoProject.Services.IChatHistoryService>(sp => sp.GetRequiredService<MyCustomUmbracoProject.Services.ChatHistoryService>());
builder.Services.AddSingleton<MyCustomUmbracoProject.Services.IMessageChannel, MyCustomUmbracoProject.Services.MistralMessageChannel>();
builder.Services.AddSingleton<MyCustomUmbracoProject.Services.IExchangeIntake, MyCustomUmbracoProject.Services.ExchangeIntake>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();


WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.AppBuilder.UseMiddleware<MyCustomUmbracoProject.Middleware.SitemapMiddleware>();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();

