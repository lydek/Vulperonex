namespace Vulperonex.Application.Expressions;

public interface ITemplateResolver
{
    string Resolve(
        string template,
        ExpressionContext context,
        TemplateResolutionOptions? options = null);
}
