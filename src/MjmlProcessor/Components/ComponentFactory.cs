using MjmlProcessor.Components.Body;
using MjmlProcessor.Parsing;
using MjmlProcessor.Rendering;

namespace MjmlProcessor.Components;

/// <summary>Maps MJML tag names onto component implementations and builds the render tree.</summary>
internal static class ComponentFactory
{
    /// <summary>Every body tag the converter understands.</summary>
    public static readonly IReadOnlyCollection<string> SupportedTags = new[]
    {
        "mj-body", "mj-wrapper", "mj-section", "mj-group", "mj-column", "mj-hero",
        "mj-text", "mj-button", "mj-image", "mj-divider", "mj-spacer", "mj-table", "mj-raw",
        "mj-social", "mj-social-element", "mj-navbar", "mj-navbar-link",
        "mj-accordion", "mj-accordion-element", "mj-accordion-title", "mj-accordion-text",
    };

    /// <summary>Builds a component tree from <paramref name="node"/>, or null for unknown tags.</summary>
    public static MjmlComponent? Build(MjmlNode node, RenderContext context, MjmlComponent? parent)
    {
        var component = Create(node, context, parent);
        if (component is null)
        {
            context.Warn(node, "Unknown element <" + node.TagName + "> was ignored.");
            return null;
        }

        component.ResolveAttributes();

        foreach (var childNode in node.Children)
        {
            var child = Build(childNode, context, component);
            if (child is not null) component.Children.Add(child);
        }

        return component;
    }

    private static MjmlComponent? Create(MjmlNode node, RenderContext context, MjmlComponent? parent) =>
        node.TagName switch
        {
            "mj-body" => new BodyComponent(node, context, parent),
            "mj-wrapper" => new WrapperComponent(node, context, parent),
            "mj-section" => new SectionComponent(node, context, parent),
            "mj-group" => new GroupComponent(node, context, parent),
            "mj-column" => new ColumnComponent(node, context, parent),
            "mj-hero" => new HeroComponent(node, context, parent),
            "mj-text" => new TextComponent(node, context, parent),
            "mj-button" => new ButtonComponent(node, context, parent),
            "mj-image" => new ImageComponent(node, context, parent),
            "mj-divider" => new DividerComponent(node, context, parent),
            "mj-spacer" => new SpacerComponent(node, context, parent),
            "mj-table" => new TableComponent(node, context, parent),
            "mj-raw" => new RawComponent(node, context, parent),
            "mj-social" => new SocialComponent(node, context, parent),
            "mj-social-element" => new SocialElementComponent(node, context, parent),
            "mj-navbar" => new NavbarComponent(node, context, parent),
            "mj-navbar-link" => new NavbarLinkComponent(node, context, parent),
            "mj-accordion" => new AccordionComponent(node, context, parent),
            "mj-accordion-element" => new AccordionElementComponent(node, context, parent),
            "mj-accordion-title" => new AccordionTitleComponent(node, context, parent),
            "mj-accordion-text" => new AccordionTextComponent(node, context, parent),
            _ => null,
        };
}
