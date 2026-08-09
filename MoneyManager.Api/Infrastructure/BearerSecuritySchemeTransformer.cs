using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace MoneyManager.Api.Infrastructure
{
    /// <summary>
    /// Describes the bearer scheme on the generated document, and says that every operation needs
    /// it.
    ///
    /// <para>
    /// The built-in generator documents what the endpoints declare, and authentication is not
    /// declared per endpoint here — it comes from the <c>FallbackPolicy</c> in <c>Program.cs</c>,
    /// which is deliberate: an endpoint that forgets <c>[Authorize]</c> is still protected. The
    /// consequence is that nothing in the metadata says so, and a document without this transformer
    /// would describe an API that needs no credentials at all.
    /// </para>
    ///
    /// <para>
    /// Applied at the document level rather than per operation for the same reason. The two
    /// genuinely anonymous endpoints — register and login — are described as requiring a token
    /// they do not need, which is the harmless direction of wrong: someone sends a header that is
    /// ignored. Marking everything optional instead would understate what the rest of the API
    /// requires.
    /// </para>
    /// </summary>
    public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        private const string SchemeId = "Bearer";

        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the token returned by /api/auth/login.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = SchemeId },
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes[SchemeId] = scheme;

            document.SecurityRequirements.Add(
                new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });

            return Task.CompletedTask;
        }
    }
}
