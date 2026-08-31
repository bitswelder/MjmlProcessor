# MjmlProcessor

A dependency-free [MJML](https://mjml.io) to HTML converter for .NET.

Give it MJML markup, get back responsive, Outlook-compatible email HTML. There is no Node.js
process to install, no `mjml` binary to shell out to, and no call to the hosted MJML API — the
whole renderer is C#, so it runs anywhere your app runs.

```csharp
using MjmlProcessor;

var html = Mjml.ToHtml("""
    <mjml>
      <mj-body>
        <mj-section>
          <mj-column>
            <mj-text>Hello world</mj-text>
          </mj-column>
        </mj-section>
      </mj-body>
    </mjml>
    """);
```

## Install

```bash
dotnet add package MjmlProcessor
```

Targets `netstandard2.0` and `net8.0`, so it works on .NET Framework 4.6.1+, .NET Core, and
current .NET.

## Usage

### One-off conversion

```csharp
string html = Mjml.ToHtml(mjmlSource);
```

### Conversion with validation warnings

`Render` returns the HTML plus anything questionable the converter found — an unknown tag, an
`mj-class` that was never declared, an image with no `src`.

```csharp
MjmlResult result = Mjml.Render(mjmlSource);

Console.WriteLine(result.Html);

foreach (MjmlWarning warning in result.Warnings)
{
    // "Line 12, column 9 (mj-image): mj-image requires a src attribute."
    Console.WriteLine(warning);
}
```

### Reuse a converter

`MjmlConverter` snapshots its options at construction and holds no per-call state, so a single
instance can be registered as a singleton and shared across threads.

```csharp
services.AddSingleton(new MjmlConverter(new MjmlOptions { Minify = true }));

// ...
string html = converter.ConvertToHtml(mjmlSource);
```

### Rendering from a file

`RenderFile` resolves `mj-include` paths relative to the file being rendered.

```csharp
string html = Mjml.FileToHtml("templates/welcome.mjml");
```

## Options

| Option | Default | What it does |
| --- | --- | --- |
| `Beautify` | `true` | Indents and line-breaks the generated markup. |
| `Minify` | `false` | Collapses the whitespace between tags. Text content is left alone. |
| `IncludeDocumentSkeleton` | `true` | Set to `false` to emit only the body markup, with no `<!doctype>` or `<head>`. CSS that needs a `<style>` block is then dropped, with a warning. |
| `Language` | `"und"` | The `lang` attribute of `<html>`. |
| `Direction` | `"auto"` | The `dir` attribute of `<html>`. |
| `ValidationLevel` | `Soft` | `Skip` ignores problems, `Soft` collects them as warnings, `Strict` throws. |
| `FileLoader` | `null` | Resolves `mj-include` paths. See below. |
| `Fonts` | Google's 5 | Font family to stylesheet URL, imported only when the family is actually used. |

```csharp
var options = new MjmlOptions
{
    Beautify = false,
    Minify = true,
    ValidationLevel = MjmlValidationLevel.Strict,
};

options.Fonts["Inter"] = "https://fonts.googleapis.com/css?family=Inter:400,700";

string html = Mjml.ToHtml(source, options);
```

### Includes

`mj-include` needs a loader, because the library never touches the filesystem on its own.
`Mjml.RenderFile` wires one up automatically; supply your own to load partials from anywhere
else — embedded resources, a database, blob storage.

```csharp
var options = new MjmlOptions { FileLoader = new DirectoryFileLoader("templates/partials") };

string html = Mjml.ToHtml(source, options);
```

`DirectoryFileLoader` refuses to read outside its configured root, appends `.mjml` when the
path has no extension, and supports `type="html"` and `type="css"` includes.

```csharp
public sealed class EmbeddedLoader : IMjmlFileLoader
{
    public string? Load(string path) => /* return the partial, or null when not found */;
}
```

## Supported elements

**Head:** `mj-head`, `mj-title`, `mj-preview`, `mj-attributes` (`mj-all`, per-tag defaults, and
named `mj-class` sets), `mj-style` (including `inline="inline"`), `mj-font`, `mj-breakpoint`,
`mj-raw`, `mj-include`.

**Layout:** `mj-body`, `mj-wrapper`, `mj-section` (including `full-width` and background
images with a VML fallback), `mj-group`, `mj-column`, `mj-hero`.

**Content:** `mj-text`, `mj-button`, `mj-image`, `mj-divider`, `mj-spacer`, `mj-table`,
`mj-raw`, `mj-social` / `mj-social-element`, `mj-navbar` / `mj-navbar-link` (including the
hamburger menu), `mj-accordion` / `mj-accordion-element` / `-title` / `-text`.

### CSS inlining

Gmail and several other clients strip `<style>` blocks, so CSS often has to live in `style`
attributes. `mj-style inline="inline"` does that for you — the rules are merged into the
matching elements and the block itself disappears:

```xml
<mj-head>
  <mj-style inline="inline">
    .card p { margin: 0 0 12px 0; font-family: Arial, sans-serif; }
    .card p:last-child { margin-bottom: 0; }
  </mj-style>
</mj-head>
```

Inlining runs over the finished document, so it reaches your own HTML inside `mj-text` and
`mj-raw` — which is usually the point — as well as elements the renderer generates and the
`<body>` element itself. The normal cascade applies: specificity, then source order, with a
`style` attribute that is already on the element beating the stylesheet unless the rule is
`!important`.

Supported selectors: type, `*`, `.class`, `#id`, attribute selectors (`[a]`, `[a=b]`, `~=`,
`|=`, `^=`, `$=`, `*=`), the descendant, `>`, `+` and `~` combinators, and the structural
pseudo-classes `:first-child`, `:last-child`, `:only-child`, `:first-of-type`, `:last-of-type`,
`:nth-child()`, `:nth-last-child()` and `:not()`.

Anything with no static equivalent — `@media`, `@font-face`, `:hover`, `::before` — cannot be
inlined and is kept in a `<style>` block instead of being dropped. A selector group is split, so
`p, a:hover { ... }` inlines the `p` half and preserves the `a:hover` half.

Two things worth knowing:

- CSS is inlined, not shortened. Declarations are merged per element; no shorthand collapsing.
- `css-class` on a component lands where MJML puts it, which is not always the element you
  expect. On `mj-button` it goes on the wrapping `<td>`, so target the link with `.cta a { ... }`
  rather than `a.cta { ... }`.

### Not implemented

- **`mj-carousel`** — the gallery component. Documents using it render without it and get a warning.
- **`mj-html-attributes`** — ignored, with a warning.

Anything unrecognised is skipped and reported as a warning rather than failing the render,
unless `ValidationLevel` is `Strict`.

## What the output looks like

The renderer produces the same shape of markup the reference MJML implementation does:

- a centred, fixed-width table layout (600px by default, set with `<mj-body width="...">`)
- Outlook and IE ghost tables in `<!--[if mso | IE]>` conditionals, so columns sit side by side
  in Word-based clients and stack everywhere else
- `@media` rules keyed to the breakpoint (`<mj-breakpoint>`), duplicated outside the media
  query for Thunderbird's `.moz-text-html` mode
- VML fallbacks for section and hero background images
- the usual client resets: `#outlook a`, `mso-table-lspace`, `-ms-interpolation-mode`, and so on

It is not a byte-for-byte match with the JavaScript implementation, so don't diff the two —
compare how they render.

## Error handling

- Syntactically broken markup throws `MjmlParseException`, carrying `Line` and `Column`.
- With `ValidationLevel.Strict`, the first semantic problem throws `MjmlException`.
- `ConvertFile` throws `FileNotFoundException` when the template is missing.

```csharp
try
{
    var html = Mjml.ToHtml(source);
}
catch (MjmlParseException ex)
{
    Console.WriteLine($"Bad template at line {ex.Line}, column {ex.Column}: {ex.Message}");
}
```

## Building

```bash
dotnet build
dotnet test
dotnet pack src/MjmlProcessor -c Release
```

## License

MIT
