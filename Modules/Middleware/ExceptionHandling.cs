using Microsoft.AspNetCore.Diagnostics;

namespace sharwapi.Core.Modules.Middleware;

/// <summary>
/// 全局异常处理中间件配置
/// </summary>
public static class ExceptionHandling
{
    /// <summary>
    /// 配置全局异常处理
    /// </summary>
    /// <param name="app">Web 应用程序</param>
    public static void Configure(WebApplication app)
    {
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                var exceptionDetails = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionDetails?.Error;
                
                if (exception != null)
                {
                    app.Logger.LogError(exception, "Unhandled exception caught by global handler at {Path}", exceptionDetails?.Path);
                }
                else
                {
                    app.Logger.LogError("Unhandled exception caught by global handler at {Path}, but exception details were unavailable.", exceptionDetails?.Path);
                }
                
                var response = new
                {
                    StatusCode = context.Response.StatusCode,
                    Message = "An unexpected internal server error has occurred.",
                    Details = app.Environment.IsDevelopment() ? exception?.Message : null,
                    Path = exceptionDetails?.Path
                };
                
                await context.Response.WriteAsJsonAsync(response);
            });
        });
    }
}
