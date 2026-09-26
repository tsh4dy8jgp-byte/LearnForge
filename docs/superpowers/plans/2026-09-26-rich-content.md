# SP2a Rich, Safe Content Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Packs gain a schema 2 source format with restricted Markdown, TeX math, tables, worked examples, misconceptions, definitions, primary sources, inline checks, modules, a glossary, language metadata and catalog metadata. Core compiles it into a typed AST that releases, snapshots, the API and accessible Angular renderers share.

**Architecture:** `ContentEngine` reads v1 sources through legacy records and a literal upcaster, and v2 sources through new source records, `RichTextCompiler` (Markdig, restricted) and `TexParser`. Both produce one release model whose rich fields are AST records with a `kind` discriminator. Stored releases carry `format: 2` plus precomputed lesson hashes; older rows are upcast on read and never rewritten. Inline-check keys live only in `Pack.Checks`, so lessons are delivery-safe by construction. The web renders the AST with recursive standalone components: no HTML strings, no parsing in the browser.

**Tech Stack:** .NET 10 / C# 14, System.Text.Json polymorphism, Markdig 1.4.0, EF Core 10 (no schema change), ASP.NET Core minimal APIs + OpenAPI 10, Angular 22 (signals, `httpResource`, `@angular/aria`), native MathML, Vitest, Playwright, `@axe-core/playwright`.

**Spec:** `docs/superpowers/specs/2026-09-26-rich-content-design.md` (read it with this plan; the spec states *what*, this plan states *how*).

## Global Constraints

- `Directory.Build.props` sets `TreatWarningsAsErrors`: any compiler warning fails the build.
- One public C# type per file, in the folders named below. Wire enums serialize camelCase via `JsonStringEnumConverter`.
- Markdig **1.4.0** is the only new package in `LearnForge.Core`. The web adds only the dev dependency `@axe-core/playwright`.
- Limits (verbatim from the spec): Markdown field 20,000 characters; TeX expression 2,000 characters; Markdown nesting 8 levels; lists nested at most 3 levels; math nesting 16 levels; 200,000 AST nodes per pack. `Json.Options.MaxDepth` = 128; source JSON keeps its depth-32 parse limit; HTTP JSON `MaxDepth` = 128.
- Every AST union uses the discriminator property `kind` with camelCase values. No derived record declares a `Kind` property.
- Diagnostic codes: `LF001`, `LF002`, `LF100`, `LF201`–`LF204`, `LF301`–`LF303`, `A11Y_LANGUAGE`, `A11Y_STRUCTURE`, `A11Y_ALTERNATIVE`. Paths use IDs, plus `:line:column` (1-based) inside a rich field.
- Identifiers match `^[a-z0-9][a-z0-9._-]{0,99}$` (existing rule).
- No database schema change and no EF migration in either provider.
- Answer keys never reach the browser while an attempt is active; inline-check keys never appear in catalog responses.
- Releases are immutable. Legacy releases, snapshots and feedback rows are read through upcasters and never rewritten.
- The web never uses `innerHTML`, `[innerHTML]`, `DomSanitizer` or `bypassSecurityTrust*`. Content renders through bindings only.
- Inline Angular templates in `src/app/rich/*` are whitespace-sensitive: do not add spaces or line breaks inside `@case (…) {…}` bodies that render text, and do not run Prettier over those files.
- After any API contract change, regenerate types with `make api-types`. Never hand-edit `apps/web/src/app/generated`. `make check-api-types` is expected to fail between Task 11 and Task 13, and must pass from Task 13 on.
- Accessibility: WCAG 2.2 AA per `docs/accessibility/*`. Every generated DOM ID is unique per page; focus rules and names follow the spec's interaction table.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **Currency in prose.** An author writes `It costs $5 and $10.` in a v2 field; it must stay literal text, not become math. Pinned in Task 4.
- **Windows line endings.** Markdown and TeX fields saved with `\r\n` must compile like `\n` (a soft break becomes a space). Pinned in Task 4.
- **A v1 pack republished after the upgrade.** Its lessons must keep the exact pre-upgrade hashes, or every learner sees "Updated since you read it". Pinned in Task 6.
- **An inline-check request naming a non-check block** (for example a text block's ID) must return 404, not a 500 or a grade. Pinned in Task 11.
- **A glossary term inside an attempt question.** Attempts have no glossary, so the term must render as its plain text with no dead disclosure button. Pinned in Task 13.

## Before You Start

- The accessibility baseline (`docs/accessibility/*`, plus the related edits to `README.md`, `docs/roadmap.md`, `docs/assessment.md`, `docs/authoring.md`, `docs/testing.md` and the learner roadmap spec) must be committed on the base branch first. The spec links to it and Task 18 edits it; a worktree created from an uncommitted state would not contain it. Ask the repository owner to commit or approve committing these files; do not commit them yourself without that approval.
- Branch from `feat/learning-record-foundation` (SP1 is not yet merged into `main`), e.g. `feat/rich-content`.
- `make setup` must have run. The Playwright task needs `make dev` running, which also builds `apps/api/bin/Debug/net10.0/LearnForge.Api.dll`.

## File Structure

**Core (`src/LearnForge.Core`)**

| Path | Responsibility |
| --- | --- |
| `Domain/Enums/TextDirection.cs`, `CourseLevel.cs` | New wire enums |
| `Domain/Rich/RichBlock.cs` + `ParagraphNode`, `ListNode`, `QuoteNode`, `CodeBlockNode` | Block AST |
| `Domain/Rich/RichInline.cs` + `TextNode`, `EmphasisNode`, `StrongNode`, `CodeNode`, `BreakNode`, `LinkNode`, `LessonLinkNode`, `TermNode`, `LangNode`, `MathInlineNode` | Inline AST |
| `Domain/Rich/MathNode.cs` + `Mi`, `Mn`, `Mo`, `Mtext`, `Mrow`, `Mfrac`, `Msqrt`, `Mroot`, `Msub`, `Msup`, `Msubsup`, `Munder`, `Mover`, `Munderover`, `Mtable`, `Mspace` | MathML-shaped AST |
| `Domain/Rich/RichText.cs` | `Literal`, `LiteralInline`, `IsBlank`, `PlainText` helpers |
| `Domain/Content/Blocks/LessonBlock.cs` + 11 block records | Release lesson blocks |
| `Domain/Content/{Module,GlossaryEntry,CatalogMetadata,InlineCheckKey,Attribution}.cs` | Release records |
| `Domain/Legacy/V1*.cs`, `ContentBlockKind.cs` | Schema 1 records, frozen |
| `Domain/Source/Source*.cs` | Schema 2 source records |
| `Services/Content/TexParser.cs`, `TexSymbols.cs`, `MathAlphabets.cs` | TeX subset → `MathNode` |
| `Services/Content/RichTextCompiler.cs` | Markdown subset → AST |
| `Services/Content/LanguageTag.cs` | BCP 47 subset validation and direction |
| `Services/Content/JsonDuplicates.cs` | Duplicate-property detection with path |
| `Services/Content/V1Upcaster.cs`, `ReleaseReader.cs` | Legacy compatibility |
| `Services/Content/PackCompiler.cs` | `SourcePack` → `Pack` |
| `Services/Content/RichContentRules.cs`, `RichWalk.cs` | Schema 2 validation |
| `Services/Content/InlineChecks.cs` | Rebuild a gradable check question |
| `Services/Content/MarkdownText.cs` | Escape literal text for `upgrade` |

**API (`apps/api`)**: `Contracts/Attempts/FeedbackDto.cs`; `Contracts/Content/{InlineCheckRequest,InlineCheckResultDto,PreviewDto,PreviewQuestion,PreviewCheck}.cs`; `Services/Attempts/{V1AttemptSnapshot,StoredFeedback}.cs`; changes to `AttemptSnapshot`, `AttemptView`, `CatalogSummaryDto`, `CourseCatalogDto`, `AttemptService`, `EvidenceBackfill`, `ReleaseCache`, `CatalogEndpoints`, `AuthoringEndpoints`, `Program.cs`.

**Web (`apps/web/src/app`)**: `rich/{context,ids,plain-text,inlines,blocks,math,code,lesson-block,inline-check}.ts` with specs; changes to `question-input.ts`, `models.ts`, `pages/{attempt,course,courses,studio}.ts`, `styles.scss`; `e2e/rich-content.spec.ts`.

**Tests (`tests/LearnForge.Tests`)**: `Fixtures/reasoning-foundations.v1.json`, `Fixtures/minimal-v2.json`, `TestPacks.cs`, and new test classes named per task.

---

### Task 1: Rich-text AST model

**Files:**
- Modify: `src/LearnForge.Core/Serialization/Json.cs`
- Create: `src/LearnForge.Core/Domain/Enums/TextDirection.cs`
- Create: `src/LearnForge.Core/Domain/Rich/*.cs` (the block, inline and math files listed in File Structure, plus `RichText.cs`)
- Test: `tests/LearnForge.Tests/RichModelTests.cs`

**Interfaces:**
- Produces: `RichBlock`, `RichInline`, `MathNode` and their records exactly as below. `RichText.Literal(string?) : RichBlock[]`, `RichText.LiteralInline(string?) : RichInline[]`, `RichText.IsBlank(RichBlock[]) / IsBlank(RichInline[]) : bool`, `RichText.PlainText(IEnumerable<RichBlock>) / PlainText(RichBlock) / PlainText(IEnumerable<RichInline>) : string`. `TextDirection { Ltr, Rtl }`.

- [ ] **Step 1: Write the failing test**

Create `tests/LearnForge.Tests/RichModelTests.cs`:

```csharp
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

public class RichModelTests
{
    private static readonly RichBlock[] Sample =
    [
        new ParagraphNode([new TextNode("A "), new EmphasisNode([new TextNode("set")]), new MathInlineNode("x^2", new Mrow([new Msup(new Mi("x"), new Mn("2"))]))]),
        new ListNode(true, 3, [[new ParagraphNode([new StrongNode([new TextNode("first")])])]]),
        new QuoteNode([new ParagraphNode([new LinkNode("https://example.org", [new TextNode("source")])])]),
        new CodeBlockNode("python", "print(1)")
    ];

    [Fact] public void Rich_text_round_trips_with_camel_case_kinds()
    {
        var json = Json.Write(Sample);
        Assert.Contains("\"kind\": \"paragraph\"", json);
        Assert.Contains("\"kind\": \"codeBlock\"", json);
        Assert.Contains("\"kind\": \"msup\"", json);
        Assert.Equal(json, Json.Write(Json.Read<RichBlock[]>(json)));
    }

    [Fact] public void Unknown_kinds_are_rejected()
    {
        Assert.Throws<JsonException>(() => Json.Read<RichBlock[]>("[{\"kind\":\"html\",\"html\":\"<b>x</b>\"}]"));
        Assert.Throws<NotSupportedException>(() => Json.Read<RichInline[]>("[{\"text\":\"no kind\"}]"));
    }

    [Fact] public void The_discriminator_may_follow_other_properties() =>
        Assert.Equal("a", Assert.IsType<TextNode>(Assert.Single(Json.Read<RichInline[]>("[{\"text\":\"a\",\"kind\":\"text\"}]"))).Text);

    [Fact] public void Plain_text_flattens_blocks_and_keeps_tex_for_math()
    {
        Assert.Equal("A setx^2\nfirst\nsource\nprint(1)", RichText.PlainText(Sample));
        Assert.True(RichText.IsBlank([new ParagraphNode([new TextNode("  ")])]));
        Assert.False(RichText.IsBlank([new ParagraphNode([new MathInlineNode("x", new Mi("x"))])]));
    }

    [Fact] public void Literal_text_is_wrapped_without_interpretation()
    {
        Assert.Empty(RichText.Literal("   "));
        var paragraph = Assert.IsType<ParagraphNode>(Assert.Single(RichText.Literal("a *b* $5")));
        Assert.Equal("a *b* $5", Assert.IsType<TextNode>(Assert.Single(paragraph.Inlines)).Text);
        Assert.Equal("x", Assert.IsType<TextNode>(Assert.Single(RichText.LiteralInline("x"))).Text);
    }

    [Fact] public void Serializer_depth_allows_nested_content() => Assert.Equal(128, Json.Options.MaxDepth);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.RichModelTests"`
Expected: build FAIL with `CS0246: The type or namespace name 'RichBlock' could not be found`.

- [ ] **Step 3: Update the serializer options**

In `src/LearnForge.Core/Serialization/Json.cs`, replace the options initializer with:

```csharp
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        // Rich-text ASTs nest; the compiler bounds nesting so every document fits. Source input keeps depth 32 in ContentEngine.
        MaxDepth = 128,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        // Authors may write "kind" after other properties.
        AllowOutOfOrderMetadataProperties = true
    };
```

- [ ] **Step 4: Create the direction enum and block AST**

`src/LearnForge.Core/Domain/Enums/TextDirection.cs`:

```csharp
namespace LearnForge.Core;

public enum TextDirection { Ltr, Rtl }
```

`src/LearnForge.Core/Domain/Rich/RichBlock.cs`:

```csharp
using System.Text.Json.Serialization;

namespace LearnForge.Core;

// Block-level rich text. Core compiles it from Markdown; browsers render it as data, never as HTML.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ParagraphNode), "paragraph")]
[JsonDerivedType(typeof(ListNode), "list")]
[JsonDerivedType(typeof(QuoteNode), "quote")]
[JsonDerivedType(typeof(CodeBlockNode), "codeBlock")]
public abstract record RichBlock;
```

`Domain/Rich/ParagraphNode.cs`:

```csharp
namespace LearnForge.Core;

public sealed record ParagraphNode(RichInline[] Inlines) : RichBlock;
```

`Domain/Rich/ListNode.cs`:

```csharp
namespace LearnForge.Core;

public sealed record ListNode(bool Ordered, int Start, RichBlock[][] Items) : RichBlock;
```

`Domain/Rich/QuoteNode.cs`:

```csharp
namespace LearnForge.Core;

public sealed record QuoteNode(RichBlock[] Blocks) : RichBlock;
```

`Domain/Rich/CodeBlockNode.cs`:

```csharp
namespace LearnForge.Core;

public sealed record CodeBlockNode(string Language, string Code) : RichBlock;
```

- [ ] **Step 5: Create the inline AST**

`Domain/Rich/RichInline.cs`:

```csharp
using System.Text.Json.Serialization;

namespace LearnForge.Core;

// Inline rich text: formatting, links, glossary terms, language spans and math.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextNode), "text")]
[JsonDerivedType(typeof(EmphasisNode), "emphasis")]
[JsonDerivedType(typeof(StrongNode), "strong")]
[JsonDerivedType(typeof(CodeNode), "code")]
[JsonDerivedType(typeof(BreakNode), "break")]
[JsonDerivedType(typeof(LinkNode), "link")]
[JsonDerivedType(typeof(LessonLinkNode), "lessonLink")]
[JsonDerivedType(typeof(TermNode), "term")]
[JsonDerivedType(typeof(LangNode), "lang")]
[JsonDerivedType(typeof(MathInlineNode), "math")]
public abstract record RichInline;
```

One file each, same namespace header `namespace LearnForge.Core;`:

```csharp
public sealed record TextNode(string Text) : RichInline;
```
```csharp
public sealed record EmphasisNode(RichInline[] Inlines) : RichInline;
```
```csharp
public sealed record StrongNode(RichInline[] Inlines) : RichInline;
```
```csharp
public sealed record CodeNode(string Code) : RichInline;
```
```csharp
public sealed record BreakNode : RichInline;
```
```csharp
// External links are HTTPS only; the compiler rejects every other scheme.
public sealed record LinkNode(string Href, RichInline[] Inlines) : RichInline;
```
```csharp
// A link to a lesson, or to one block of it, in the same pack.
public sealed record LessonLinkNode(string LessonId, string? BlockId, RichInline[] Inlines) : RichInline;
```
```csharp
// A reference to a glossary entry; the browser shows its definition on request.
public sealed record TermNode(string TermId, RichInline[] Inlines) : RichInline;
```
```csharp
// A span in another language. The compiler derives Direction from the tag.
public sealed record LangNode(string Language, TextDirection Direction, RichInline[] Inlines) : RichInline;
```
```csharp
// Tex is the author's source, kept for plain text and previews; Root is what browsers render.
public sealed record MathInlineNode(string Tex, MathNode Root) : RichInline;
```

(File names: `TextNode.cs`, `EmphasisNode.cs`, `StrongNode.cs`, `CodeNode.cs`, `BreakNode.cs`, `LinkNode.cs`, `LessonLinkNode.cs`, `TermNode.cs`, `LangNode.cs`, `MathInlineNode.cs`.)

- [ ] **Step 6: Create the math AST**

`Domain/Rich/MathNode.cs`:

```csharp
using System.Text.Json.Serialization;

namespace LearnForge.Core;

// A MathML-shaped tree produced by TexParser. Kinds mirror MathML Core element names.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Mi), "mi")]
[JsonDerivedType(typeof(Mn), "mn")]
[JsonDerivedType(typeof(Mo), "mo")]
[JsonDerivedType(typeof(Mtext), "mtext")]
[JsonDerivedType(typeof(Mrow), "mrow")]
[JsonDerivedType(typeof(Mfrac), "mfrac")]
[JsonDerivedType(typeof(Msqrt), "msqrt")]
[JsonDerivedType(typeof(Mroot), "mroot")]
[JsonDerivedType(typeof(Msub), "msub")]
[JsonDerivedType(typeof(Msup), "msup")]
[JsonDerivedType(typeof(Msubsup), "msubsup")]
[JsonDerivedType(typeof(Munder), "munder")]
[JsonDerivedType(typeof(Mover), "mover")]
[JsonDerivedType(typeof(Munderover), "munderover")]
[JsonDerivedType(typeof(Mtable), "mtable")]
[JsonDerivedType(typeof(Mspace), "mspace")]
public abstract record MathNode;
```

One file each (`Mi.cs` … `Mspace.cs`), header `namespace LearnForge.Core;`:

```csharp
// Normal renders upright (mathvariant="normal"): function names, \mathrm and capital Greek.
public sealed record Mi(string Text, bool Normal = false) : MathNode;
```
```csharp
public sealed record Mn(string Text) : MathNode;
```
```csharp
public sealed record Mo(string Text, bool Stretchy = false, bool LargeOp = false) : MathNode;
```
```csharp
public sealed record Mtext(string Text) : MathNode;
```
```csharp
public sealed record Mrow(MathNode[] Children) : MathNode;
```
```csharp
public sealed record Mfrac(MathNode Numerator, MathNode Denominator) : MathNode;
```
```csharp
public sealed record Msqrt(MathNode Child) : MathNode;
```
```csharp
public sealed record Mroot(MathNode Base, MathNode Index) : MathNode;
```
```csharp
public sealed record Msub(MathNode Base, MathNode Subscript) : MathNode;
```
```csharp
public sealed record Msup(MathNode Base, MathNode Superscript) : MathNode;
```
```csharp
public sealed record Msubsup(MathNode Base, MathNode Subscript, MathNode Superscript) : MathNode;
```
```csharp
public sealed record Munder(MathNode Base, MathNode Under) : MathNode;
```
```csharp
public sealed record Mover(MathNode Base, MathNode Over, bool Accent = false) : MathNode;
```
```csharp
public sealed record Munderover(MathNode Base, MathNode Under, MathNode Over) : MathNode;
```
```csharp
// ColumnAlign uses MathML syntax, e.g. "left left" for cases.
public sealed record Mtable(MathNode[][] Rows, string? ColumnAlign = null) : MathNode;
```
```csharp
public sealed record Mspace(string Width) : MathNode;
```

- [ ] **Step 7: Create the helpers**

`Domain/Rich/RichText.cs`:

```csharp
namespace LearnForge.Core;

public static class RichText
{
    // Schema 1 text is literal: wrap it, never interpret it as Markdown.
    public static RichBlock[] Literal(string? text) => string.IsNullOrWhiteSpace(text) ? [] : [new ParagraphNode([new TextNode(text)])];

    public static RichInline[] LiteralInline(string? text) => string.IsNullOrEmpty(text) ? [] : [new TextNode(text)];

    public static bool IsBlank(RichBlock[] blocks) => string.IsNullOrWhiteSpace(PlainText(blocks));

    public static bool IsBlank(RichInline[] inlines) => string.IsNullOrWhiteSpace(PlainText(inlines));

    public static string PlainText(IEnumerable<RichBlock> blocks) => string.Join("\n", blocks.Select(block => PlainText(block)));

    public static string PlainText(RichBlock block) => block switch
    {
        ParagraphNode p => PlainText(p.Inlines),
        ListNode l => string.Join("\n", l.Items.Select(item => PlainText(item))),
        QuoteNode q => PlainText(q.Blocks),
        CodeBlockNode c => c.Code,
        _ => ""
    };

    // Math contributes its TeX source, so search and labels keep the expression.
    public static string PlainText(IEnumerable<RichInline> inlines) => string.Concat(inlines.Select(node => node switch
    {
        TextNode t => t.Text,
        CodeNode c => c.Code,
        BreakNode => "\n",
        MathInlineNode m => m.Tex,
        EmphasisNode e => PlainText(e.Inlines),
        StrongNode s => PlainText(s.Inlines),
        LinkNode l => PlainText(l.Inlines),
        LessonLinkNode l => PlainText(l.Inlines),
        TermNode t => PlainText(t.Inlines),
        LangNode g => PlainText(g.Inlines),
        _ => ""
    }));
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.RichModelTests"`
Expected: PASS (6 tests). Then run `dotnet test LearnForge.slnx` and confirm the whole suite still passes.

- [ ] **Step 9: Commit**

```bash
git add src/LearnForge.Core tests/LearnForge.Tests/RichModelTests.cs
git commit -m "feat(core): add the rich-text AST with kind discriminators

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Language tags and direction

**Files:**
- Create: `src/LearnForge.Core/Services/Content/LanguageTag.cs`
- Test: `tests/LearnForge.Tests/LanguageTagTests.cs`

**Interfaces:**
- Consumes: `TextDirection` (Task 1).
- Produces: `LanguageTag.IsValid([NotNullWhen(true)] string? tag) : bool`, `LanguageTag.DirectionOf(string tag) : TextDirection`.

- [ ] **Step 1: Write the failing test**

```csharp
using Xunit;

namespace LearnForge.Tests;

public class LanguageTagTests
{
    [Theory]
    [InlineData("en")][InlineData("pt-BR")][InlineData("zh-Hant")][InlineData("es-419")][InlineData("sr-Cyrl-RS")]
    [InlineData("grc")][InlineData("fil")][InlineData("la")]
    public void Canonical_tags_with_known_languages_are_valid(string tag) => Assert.True(LanguageTag.IsValid(tag));

    [Theory]
    [InlineData(null)][InlineData("")][InlineData("EN")][InlineData("en_US")][InlineData("en-us")][InlineData("english")]
    [InlineData("xx")][InlineData("und")][InlineData("zh-hant")][InlineData("en-US-extra")]
    public void Malformed_or_unknown_tags_are_invalid(string? tag) => Assert.False(LanguageTag.IsValid(tag));

    [Theory]
    [InlineData("ar", TextDirection.Rtl)][InlineData("he", TextDirection.Rtl)][InlineData("fa-IR", TextDirection.Rtl)]
    [InlineData("ur", TextDirection.Rtl)][InlineData("yi", TextDirection.Rtl)][InlineData("pa-Arab", TextDirection.Rtl)]
    [InlineData("en", TextDirection.Ltr)][InlineData("az-Latn", TextDirection.Ltr)][InlineData("sd-Deva", TextDirection.Ltr)]
    public void Direction_follows_the_script_then_the_language(string tag, TextDirection expected) =>
        Assert.Equal(expected, LanguageTag.DirectionOf(tag));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.LanguageTagTests"`
Expected: build FAIL, `LanguageTag` not found.

- [ ] **Step 3: Implement**

`src/LearnForge.Core/Services/Content/LanguageTag.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace LearnForge.Core;

// A deterministic BCP 47 subset: language[-Script][-REGION] in canonical case. The language must be a known
// ISO 639 code, from an embedded table, so compilation never depends on the host's ICU data.
public static partial class LanguageTag
{
    private static readonly HashSet<string> Languages = [..
        ("aa ab ae af ak am an ar as av ay az ba be bg bi bm bn bo br bs ca ce ch co cr cs cu cv cy da de dv dz ee el en eo es et eu " +
         "fa ff fi fj fo fr fy ga gd gl gn gu gv ha he hi ho hr ht hu hy hz ia id ie ig ii ik io is it iu ja jv ka kg ki kj kk kl km kn " +
         "ko kr ks ku kv kw ky la lb lg li ln lo lt lu lv mg mh mi mk ml mn mr ms mt my na nb nd ne ng nl nn no nr nv ny oc oj om or " +
         "os pa pi pl ps pt qu rm rn ro ru rw sa sc sd se sg si sk sl sm sn so sq sr ss st su sv sw ta te tg th ti tk tl tn to tr ts " +
         "tt tw ty ug uk ur uz ve vi vo wa wo xh yi yo za zh zu " +
         // Three-letter ISO 639-2/3 codes for languages without a two-letter code, including historical languages.
         "akk ang arc ast bal ban bem bho bug ceb chr ckb cop crh dsb egy enm fil fro fur gil gmh goh got grc gsw haw hil hmn hsb " +
         "ilo jbo kab kbd kha kok krl lad lkt lzh mad mag mai men mfe mni moh mos mus nah nap nds new non nso pag pam pap pau peo " +
         "prs rom sah sat scn sco sga shn sid sma smj smn sms srn sux syr szl tet tig tkl tlh tpi tum tvl udm uga vai wal war yue zza")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    private static readonly HashSet<string> RightToLeftLanguages = ["ar", "arc", "ckb", "dv", "fa", "he", "ks", "prs", "ps", "sd", "syr", "ug", "ur", "yi"];
    private static readonly HashSet<string> RightToLeftScripts = ["Adlm", "Arab", "Hebr", "Nkoo", "Syrc", "Thaa"];

    [GeneratedRegex(@"^(?<language>[a-z]{2,3})(?:-(?<script>[A-Z][a-z]{3}))?(?:-(?<region>[A-Z]{2}|[0-9]{3}))?$")]
    private static partial Regex Pattern();

    public static bool IsValid([NotNullWhen(true)] string? tag) =>
        tag is not null && Pattern().Match(tag) is { Success: true } match && Languages.Contains(match.Groups["language"].Value);

    public static TextDirection DirectionOf(string tag)
    {
        var match = Pattern().Match(tag);
        if (!match.Success) return TextDirection.Ltr;
        var rtl = match.Groups["script"].Success
            ? RightToLeftScripts.Contains(match.Groups["script"].Value)
            : RightToLeftLanguages.Contains(match.Groups["language"].Value);
        return rtl ? TextDirection.Rtl : TextDirection.Ltr;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.LanguageTagTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/LearnForge.Core/Services/Content/LanguageTag.cs tests/LearnForge.Tests/LanguageTagTests.cs
git commit -m "feat(core): validate language tags and derive text direction

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: TeX subset parser

**Files:**
- Create: `src/LearnForge.Core/Services/Content/TexParser.cs`, `TexSymbols.cs`, `MathAlphabets.cs`
- Test: `tests/LearnForge.Tests/TexParserTests.cs`

**Interfaces:**
- Consumes: `MathNode` records (Task 1).
- Changes: `Diagnostic` gains an optional fourth parameter, `string? Guidance = null` (Step 3). Existing three-argument constructions keep compiling.
- Produces: `TexParser.Parse(string tex, string path, List<Diagnostic> diagnostics, int line = 0, int column = 0) : MathNode`. It always returns an `Mrow` (an empty `Mrow` after an error) and adds at most one diagnostic per expression. `line` and `column` are zero-based offsets of the expression inside its field.

- [ ] **Step 1: Write the failing test**

```csharp
using Xunit;

namespace LearnForge.Tests;

public class TexParserTests
{
    private static MathNode Parse(string tex)
    {
        var diagnostics = new List<Diagnostic>();
        var node = TexParser.Parse(tex, "f", diagnostics);
        Assert.Empty(diagnostics);
        return node;
    }

    private static Diagnostic Fail(string tex, int line = 0, int column = 0)
    {
        var diagnostics = new List<Diagnostic>();
        Assert.Equal(Json.Write<MathNode>(new Mrow([])), Json.Write(TexParser.Parse(tex, "f", diagnostics, line, column)));
        return Assert.Single(diagnostics);
    }

    // Records holding arrays compare by reference, so compare serialized trees.
    private static void Same(MathNode expected, MathNode actual) => Assert.Equal(Json.Write(expected), Json.Write(actual));

    [Fact] public void Atoms_become_identifiers_numbers_and_operators() =>
        Same(new Mrow([new Mi("x"), new Mo("+"), new Mn("2.5"), new Mo("−"), new Mi("y")]), Parse("x + 2.5 - y"));

    [Fact] public void Fractions_and_roots() =>
        Same(new Mrow([new Mfrac(new Mi("a"), new Mi("b")), new Mo("+"), new Mroot(new Mi("x"), new Mn("3")), new Msqrt(new Mn("2"))]),
            Parse(@"\frac{a}{b} + \sqrt[3]{x}\sqrt2"));

    [Fact] public void Scripts_take_one_token_or_a_group()
    {
        Same(new Mrow([new Msubsup(new Mi("x"), new Mi("i"), new Mn("2"))]), Parse("x_i^2"));
        Same(new Mrow([new Msup(new Mi("x"), new Mn("1")), new Mn("0")]), Parse("x^10"));
        Same(new Mrow([new Msup(new Mi("x"), new Mn("10"))]), Parse("x^{10}"));
        Same(new Mrow([new Msup(new Mi("f"), new Mo("′")), new Mo("("), new Mi("x"), new Mo(")")]), Parse("f'(x)"));
    }

    [Fact] public void Large_operators_place_limits()
    {
        Same(new Mrow([new Munderover(new Mo("∑", LargeOp: true), new Mrow([new Mi("i"), new Mo("="), new Mn("1")]), new Mi("n")), new Mi("i")]),
            Parse(@"\sum_{i=1}^{n} i"));
        Same(new Mrow([new Msubsup(new Mo("∫", LargeOp: true), new Mn("0"), new Mn("1")), new Mi("x")]), Parse(@"\int_0^1 x"));
    }

    [Fact] public void Functions_are_upright_and_followed_by_function_application()
    {
        Same(new Mrow([new Mi("sin", true), new Mo("⁡"), new Mi("x")]), Parse(@"\sin x"));
        Same(new Mrow([new Msub(new Mi("log", true), new Mn("2")), new Mo("⁡"), new Mi("x")]), Parse(@"\log_2 x"));
    }

    [Fact] public void Fences_stretch() =>
        Same(new Mrow([new Mrow([new Mo("(", true), new Mrow([new Mfrac(new Mn("1"), new Mn("2"))]), new Mo(")", true)])]),
            Parse(@"\left( \frac{1}{2} \right)"));

    [Fact] public void Letters_symbols_styles_and_text()
    {
        Same(new Mrow([new Mi("α"), new Mi("Ω", true), new Mi("ℝ"), new Mi("𝐯"), new Mi("d", true)]), Parse(@"\alpha \Omega \mathbb{R} \mathbf{v} \mathrm{d}"));
        Same(new Mrow([new Mo("{"), new Mn("1"), new Mo("}"), new Mo("∪"), new Mi("∅", true)]), Parse(@"\{1\} \cup \emptyset"));
        Same(new Mrow([new Mi("x"), new Mtext(" if "), new Mi("y")]), Parse(@"x \text{ if } y"));
        Same(new Mrow([new Mover(new Mi("v"), new Mo("→"), true), new Mspace("1em")]), Parse(@"\vec{v}\quad"));
    }

    [Fact] public void Environments_become_tables()
    {
        Same(new Mrow([new Mrow([new Mo("(", true), new Mtable([[new Mn("1"), new Mn("0")], [new Mn("0"), new Mn("1")]]), new Mo(")", true)])]),
            Parse(@"\begin{pmatrix} 1 & 0 \\ 0 & 1 \end{pmatrix}"));
        var cases = Assert.IsType<Mrow>(Assert.IsType<Mrow>(Parse(@"\begin{cases} 1 & x > 0 \\ 0 & \text{otherwise} \\ \end{cases}")).Children[0]);
        var table = Assert.IsType<Mtable>(cases.Children[1]);
        Assert.Equal("left left", table.ColumnAlign);
        Assert.Equal(2, table.Rows.Length);
    }

    [Fact] public void Whitespace_including_windows_line_endings_is_ignored() =>
        Same(new Mrow([new Mi("a"), new Mo("="), new Mi("b")]), Parse("a\r\n=\r\nb"));

    [Theory]
    [InlineData(@"\foo", "LF301", "f:1:1")]
    [InlineData(@"\frac{a}{b", "LF302", "f:1:11")]
    [InlineData("a & b", "LF302", "f:1:3")]
    [InlineData("x^", "LF302", "f:1:3")]
    [InlineData(@"\left( x", "LF302", "f:1:9")]
    [InlineData(@"\begin{matrix} 1 \end{pmatrix}", "LF302", "f:1:18")]
    [InlineData(@"\begin{align} x \end{align}", "LF301", "f:1:1")]
    [InlineData("x_1_2", "LF302", "f:1:4")]
    [InlineData("a # b", "LF301", "f:1:3")]
    public void Unsupported_input_is_reported_with_its_position(string tex, string code, string path)
    {
        var diagnostic = Fail(tex);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(path, diagnostic.Path);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Guidance));
    }

    [Fact] public void Positions_are_offset_by_the_enclosing_field() =>
        Assert.Equal("f:3:10", Fail(@"x + \foo", line: 2, column: 5).Path);

    [Fact] public void Limits_are_enforced()
    {
        Assert.Equal("LF303", Fail(new string('x', TexParser.MaxLength + 1)).Code);
        Assert.Equal("LF303", Fail(new string('{', 17) + "x" + new string('}', 17)).Code);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.TexParserTests"`
Expected: build FAIL, `TexParser` not found.

- [ ] **Step 3: Add guidance to diagnostics**

Replace `src/LearnForge.Core/Domain/Diagnostics/Diagnostic.cs`:

```csharp
namespace LearnForge.Core;

// Guidance tells the author how to fix the problem in plain language.
public sealed record Diagnostic(string Code, string Path, string Message, string? Guidance = null);
```

- [ ] **Step 4: Add the symbol tables and alphabets**

`src/LearnForge.Core/Services/Content/TexSymbols.cs`:

```csharp
namespace LearnForge.Core;

// Command tables for TexParser. Each entry maps TeX to the Unicode character MathML renders.
internal static class TexSymbols
{
    public static readonly Dictionary<char, string> Characters = new()
    {
        ['+'] = "+", ['-'] = "−", ['='] = "=", ['<'] = "<", ['>'] = ">", ['('] = "(", [')'] = ")", ['['] = "[", [']'] = "]",
        [','] = ",", [';'] = ";", [':'] = ":", ['!'] = "!", ['/'] = "/", ['|'] = "|", ['.'] = ".", ['?'] = "?", ['*'] = "∗", ['\''] = "′"
    };

    public static readonly Dictionary<string, string> Greek = new()
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ϵ", ["varepsilon"] = "ε", ["zeta"] = "ζ",
        ["eta"] = "η", ["theta"] = "θ", ["vartheta"] = "ϑ", ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ",
        ["nu"] = "ν", ["xi"] = "ξ", ["pi"] = "π", ["varpi"] = "ϖ", ["rho"] = "ρ", ["varrho"] = "ϱ", ["sigma"] = "σ",
        ["varsigma"] = "ς", ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "ϕ", ["varphi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ",
        ["omega"] = "ω", ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ", ["Pi"] = "Π",
        ["Sigma"] = "Σ", ["Upsilon"] = "Υ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω"
    };

    public static readonly Dictionary<string, string> Identifiers = new() { ["ell"] = "ℓ", ["infty"] = "∞", ["emptyset"] = "∅" };

    public static readonly Dictionary<string, string> Operators = new()
    {
        ["times"] = "×", ["cdot"] = "⋅", ["div"] = "÷", ["pm"] = "±", ["mp"] = "∓", ["le"] = "≤", ["leq"] = "≤", ["ge"] = "≥",
        ["geq"] = "≥", ["neq"] = "≠", ["ne"] = "≠", ["approx"] = "≈", ["equiv"] = "≡", ["sim"] = "∼", ["propto"] = "∝",
        ["in"] = "∈", ["notin"] = "∉", ["subset"] = "⊂", ["subseteq"] = "⊆", ["supset"] = "⊃", ["supseteq"] = "⊇",
        ["cup"] = "∪", ["cap"] = "∩", ["setminus"] = "∖", ["partial"] = "∂", ["nabla"] = "∇", ["to"] = "→",
        ["rightarrow"] = "→", ["leftarrow"] = "←", ["Rightarrow"] = "⇒", ["Leftarrow"] = "⇐", ["Leftrightarrow"] = "⇔",
        ["iff"] = "⟺", ["mapsto"] = "↦", ["forall"] = "∀", ["exists"] = "∃", ["neg"] = "¬", ["land"] = "∧", ["lor"] = "∨",
        ["ldots"] = "…", ["cdots"] = "⋯", ["circ"] = "∘", ["angle"] = "∠", ["perp"] = "⊥", ["parallel"] = "∥",
        ["degree"] = "°", ["langle"] = "⟨", ["rangle"] = "⟩"
    };

    public static readonly Dictionary<string, (string Text, bool Limits)> LargeOperators = new()
    {
        ["sum"] = ("∑", true), ["prod"] = ("∏", true), ["lim"] = ("lim", true),
        ["int"] = ("∫", false), ["iint"] = ("∬", false), ["oint"] = ("∮", false)
    };

    public static readonly HashSet<string> Functions =
        ["sin", "cos", "tan", "sec", "csc", "cot", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "log", "ln", "exp", "max", "min", "det", "gcd", "deg"];

    public static readonly Dictionary<string, string> Accents = new()
    {
        ["vec"] = "→", ["hat"] = "^", ["bar"] = "¯", ["dot"] = "˙", ["ddot"] = "¨", ["tilde"] = "˜"
    };

    public static readonly Dictionary<string, string> Spaces = new()
    {
        [","] = "0.1667em", [":"] = "0.2222em", [";"] = "0.2778em", [" "] = "0.25em", ["quad"] = "1em", ["qquad"] = "2em"
    };

    public static readonly (string Command, string Symbol)[] Delimiters =
        [("\\{", "{"), ("\\}", "}"), ("\\|", "‖"), ("\\langle", "⟨"), ("\\rangle", "⟩")];

    public static readonly HashSet<string> Environments = ["matrix", "pmatrix", "bmatrix", "vmatrix", "cases", "aligned"];
}
```

`src/LearnForge.Core/Services/Content/MathAlphabets.cs`:

```csharp
namespace LearnForge.Core;

// MathML Core styles letters with Unicode mathematical alphanumerics rather than mathvariant values.
internal static class MathAlphabets
{
    public static string Bold(string text) => Map(text, c => c switch
    {
        >= 'A' and <= 'Z' => 0x1D400 + c - 'A', >= 'a' and <= 'z' => 0x1D41A + c - 'a', >= '0' and <= '9' => 0x1D7CE + c - '0', _ => c
    });

    public static string Italic(string text) => Map(text, c => c switch
    {
        'h' => 0x210E, >= 'A' and <= 'Z' => 0x1D434 + c - 'A', >= 'a' and <= 'z' => 0x1D44E + c - 'a', _ => c
    });

    public static string DoubleStruck(string text) => Map(text, c => c switch
    {
        'C' => 0x2102, 'H' => 0x210D, 'N' => 0x2115, 'P' => 0x2119, 'Q' => 0x211A, 'R' => 0x211D, 'Z' => 0x2124,
        >= 'A' and <= 'Z' => 0x1D538 + c - 'A', >= 'a' and <= 'z' => 0x1D552 + c - 'a', >= '0' and <= '9' => 0x1D7D8 + c - '0', _ => c
    });

    public static string Script(string text) => Map(text, c => c switch
    {
        'B' => 0x212C, 'E' => 0x2130, 'F' => 0x2131, 'H' => 0x210B, 'I' => 0x2110, 'L' => 0x2112, 'M' => 0x2133, 'R' => 0x211B,
        'e' => 0x212F, 'g' => 0x210A, 'o' => 0x2134,
        >= 'A' and <= 'Z' => 0x1D49C + c - 'A', >= 'a' and <= 'z' => 0x1D4B6 + c - 'a', _ => c
    });

    private static string Map(string text, Func<char, int> map) => string.Concat(text.Select(c => char.ConvertFromUtf32(map(c))));
}
```

- [ ] **Step 5: Implement the parser**

`src/LearnForge.Core/Services/Content/TexParser.cs`:

```csharp
using System.Text;

namespace LearnForge.Core;

// Parses LearnForge's TeX subset into MathML-shaped nodes. Anything outside the subset becomes a
// diagnostic; nothing is passed through to the browser as markup.
public sealed class TexParser
{
    public const int MaxLength = 2_000;
    public const int MaxDepth = 16;

    private enum Stop { End, Group, Bracket, Right, Cell }

    private sealed class Failure(string code, int at, string message, string guidance) : Exception(message)
    {
        public string Code { get; } = code;
        public int At { get; } = at;
        public string Guidance { get; } = guidance;
    }

    private readonly string tex;
    private int pos;
    private int depth;

    private TexParser(string tex) => this.tex = tex;

    // line and column (zero-based) locate the expression inside its field, so reported positions match the source.
    public static MathNode Parse(string tex, string path, List<Diagnostic> diagnostics, int line = 0, int column = 0)
    {
        if (tex.Length > MaxLength)
        {
            diagnostics.Add(new("LF303", $"{path}:{line + 1}:{column + 1}", $"Math is limited to {MaxLength} characters.", "Split the expression or move part of it into text."));
            return new Mrow([]);
        }
        try { return new TexParser(tex).Row(Stop.End); }
        catch (Failure failure)
        {
            var before = tex[..Math.Min(failure.At, tex.Length)];
            var lines = before.Count(c => c == '\n');
            var col = lines == 0 ? column + before.Length : before.Length - before.LastIndexOf('\n') - 1;
            diagnostics.Add(new(failure.Code, $"{path}:{line + lines + 1}:{col + 1}", failure.Message, failure.Guidance));
            return new Mrow([]);
        }
    }

    private Mrow Row(Stop stop)
    {
        if (++depth > MaxDepth) throw new Failure("LF303", pos, "Math is nested too deeply.", $"Keep groups, fractions and scripts within {MaxDepth} levels.");
        var items = new List<MathNode>();
        while (true)
        {
            SkipSpaces();
            if (pos >= tex.Length)
            {
                if (stop == Stop.End) break;
                throw new Failure("LF302", pos, stop switch
                {
                    Stop.Group => "A { group is not closed.",
                    Stop.Bracket => "A \\sqrt[ index is not closed.",
                    Stop.Right => "\\left has no matching \\right.",
                    _ => "An environment is not closed."
                }, "Add the missing closing brace, bracket, \\right or \\end.");
            }
            var c = tex[pos];
            if (c == '}')
            {
                if (stop == Stop.Group) break;
                throw new Failure("LF302", pos, "Unexpected closing brace.", "Remove it or add the matching {.");
            }
            if (c == ']' && stop == Stop.Bracket) break;
            if (c == '&' || IsDoubleBackslash() || IsWord("end"))
            {
                if (stop == Stop.Cell) break;
                throw new Failure("LF302", pos, "&, \\\\ and \\end belong inside an environment.", "Wrap rows in \\begin{matrix} … \\end{matrix}, cases or aligned.");
            }
            if (IsWord("right"))
            {
                if (stop == Stop.Right) break;
                throw new Failure("LF302", pos, "\\right has no matching \\left.", "Add \\left before the opening delimiter.");
            }
            var (node, function) = Scripted();
            items.Add(node);
            if (function) items.Add(new Mo("⁡")); // function application, read by screen readers
        }
        depth--;
        return new Mrow(items.ToArray());
    }

    private (MathNode Node, bool Function) Scripted()
    {
        var node = Primary(out var limits);
        var function = node is Mi { Normal: true } mi && TexSymbols.Functions.Contains(mi.Text);
        SkipSpaces();
        while (pos < tex.Length && tex[pos] == '\'')
        {
            pos++;
            node = new Msup(node, new Mo("′"));
            SkipSpaces();
        }
        MathNode? sub = null, sup = null;
        while (pos < tex.Length && tex[pos] is '^' or '_')
        {
            var superscript = tex[pos] == '^';
            var at = pos++;
            if ((superscript ? sup : sub) is not null)
                throw new Failure("LF302", at, superscript ? "Double superscript." : "Double subscript.", "Group the script with braces, e.g. x^{ab}.");
            var argument = Argument();
            if (superscript) sup = argument; else sub = argument;
            SkipSpaces();
        }
        if (sub is null && sup is null) return (node, function);
        MathNode scripted = limits
            ? (sub, sup) switch { ({ } b, { } p) => new Munderover(node, b, p), ({ } b, null) => new Munder(node, b), _ => new Mover(node, sup!) }
            : (sub, sup) switch { ({ } b, { } p) => new Msubsup(node, b, p), ({ } b, null) => new Msub(node, b), _ => new Msup(node, sup!) };
        return (scripted, function);
    }

    private MathNode Argument()
    {
        SkipSpaces();
        if (pos >= tex.Length) throw new Failure("LF302", pos, "A command or script is missing its argument.", "Add the argument in braces, e.g. x^{2}.");
        if (tex[pos] == '{')
        {
            pos++;
            var group = Row(Stop.Group);
            pos++;
            return Single(group);
        }
        if (char.IsAsciiDigit(tex[pos])) return new Mn(tex[pos++].ToString()); // TeX takes one digit: x^10 is x¹0
        return Primary(out _);
    }

    private static MathNode Single(Mrow row) => row.Children.Length == 1 ? row.Children[0] : row;

    private MathNode Primary(out bool limits)
    {
        limits = false;
        SkipSpaces();
        if (pos >= tex.Length) throw new Failure("LF302", pos, "Expected a value.", "Complete the expression.");
        var c = tex[pos];
        if (c == '{')
        {
            pos++;
            var group = Row(Stop.Group);
            pos++;
            return group;
        }
        if (c == '\\') return Command(out limits);
        if (char.IsAsciiLetter(c))
        {
            pos++;
            return new Mi(c.ToString());
        }
        if (char.IsAsciiDigit(c) || (c == '.' && pos + 1 < tex.Length && char.IsAsciiDigit(tex[pos + 1]))) return Number();
        if (TexSymbols.Characters.TryGetValue(c, out var op))
        {
            pos++;
            return new Mo(op);
        }
        if (c == '~')
        {
            pos++;
            return new Mspace("0.2778em");
        }
        if (c > 127)
        {
            var rune = Rune.GetRuneAt(tex, pos);
            pos += rune.Utf16SequenceLength;
            return Rune.IsLetter(rune) ? new Mi(rune.ToString()) : Rune.IsDigit(rune) ? new Mn(rune.ToString()) : new Mo(rune.ToString());
        }
        throw new Failure("LF301", pos, $"The character '{c}' is not supported in math.",
            c == '$' ? "Close inline math with a single $." : "Write it inside \\text{…} or escape it with a backslash.");
    }

    private Mn Number()
    {
        var start = pos;
        while (pos < tex.Length && char.IsAsciiDigit(tex[pos])) pos++;
        if (pos + 1 < tex.Length && tex[pos] == '.' && char.IsAsciiDigit(tex[pos + 1]))
        {
            pos++;
            while (pos < tex.Length && char.IsAsciiDigit(tex[pos])) pos++;
        }
        return new Mn(tex[start..pos]);
    }

    private MathNode Command(out bool limits)
    {
        limits = false;
        var start = pos++;
        if (pos >= tex.Length) throw new Failure("LF301", start, "A backslash must be followed by a command.", "Remove the trailing backslash.");
        string name;
        if (char.IsAsciiLetter(tex[pos]))
        {
            var from = pos;
            while (pos < tex.Length && char.IsAsciiLetter(tex[pos])) pos++;
            name = tex[from..pos];
        }
        else name = tex[pos++].ToString();
        switch (name)
        {
            case "frac" or "dfrac": return new Mfrac(Argument(), Argument());
            case "sqrt":
                SkipSpaces();
                if (pos < tex.Length && tex[pos] == '[')
                {
                    pos++;
                    var index = Single(Row(Stop.Bracket));
                    pos++;
                    return new Mroot(Argument(), index);
                }
                return new Msqrt(Argument());
            case "left": return Fenced();
            case "begin": return Environment(start);
            case "text": return new Mtext(TextArgument());
            case "mathrm": return Styled(Argument(), null);
            case "mathbf": return Styled(Argument(), MathAlphabets.Bold);
            case "mathit": return Styled(Argument(), MathAlphabets.Italic);
            case "mathbb": return Styled(Argument(), MathAlphabets.DoubleStruck);
            case "mathcal": return Styled(Argument(), MathAlphabets.Script);
            case "overline": return new Mover(Argument(), new Mo("‾", Stretchy: true), true);
            case "underline": return new Munder(Argument(), new Mo("_", Stretchy: true));
            case "{": return new Mo("{");
            case "}": return new Mo("}");
            case "|": return new Mo("‖");
            case "$" or "%" or "&" or "#" or "_": return new Mo(name);
            case "!": return new Mrow([]); // negative space is not supported by MathML Core
        }
        if (TexSymbols.Accents.TryGetValue(name, out var accent)) return new Mover(Argument(), new Mo(accent), true);
        if (TexSymbols.Spaces.TryGetValue(name, out var width)) return new Mspace(width);
        if (TexSymbols.Greek.TryGetValue(name, out var greek)) return new Mi(greek, char.IsUpper(name[0]));
        if (TexSymbols.Identifiers.TryGetValue(name, out var identifier)) return new Mi(identifier, true);
        if (TexSymbols.Operators.TryGetValue(name, out var op)) return new Mo(op);
        if (TexSymbols.LargeOperators.TryGetValue(name, out var large))
        {
            limits = large.Limits;
            return new Mo(large.Text, LargeOp: large.Text.Length == 1);
        }
        if (TexSymbols.Functions.Contains(name)) return new Mi(name, true);
        throw new Failure("LF301", start, $"\\{name} is not supported.", "See the TeX subset in the authoring guide.");
    }

    private MathNode Fenced()
    {
        var open = Delimiter();
        var body = Row(Stop.Right);
        pos += "\\right".Length;
        var close = Delimiter();
        var children = new List<MathNode>();
        if (open is not null) children.Add(new Mo(open, Stretchy: true));
        children.Add(body);
        if (close is not null) children.Add(new Mo(close, Stretchy: true));
        return new Mrow(children.ToArray());
    }

    private string? Delimiter()
    {
        SkipSpaces();
        var at = pos;
        if (pos < tex.Length)
        {
            var c = tex[pos];
            if (c is '(' or ')' or '[' or ']' or '|')
            {
                pos++;
                return c.ToString();
            }
            if (c == '.')
            {
                pos++;
                return null;
            }
            foreach (var (command, symbol) in TexSymbols.Delimiters)
                if (At(command))
                {
                    pos += command.Length;
                    return symbol;
                }
        }
        throw new Failure("LF302", at, "\\left and \\right need a delimiter.", "Use ( ) [ ] \\{ \\} | \\langle \\rangle, or . for none.");
    }

    private MathNode Environment(int start)
    {
        var name = EnvironmentName();
        if (!TexSymbols.Environments.Contains(name))
            throw new Failure("LF301", start, $"The {name} environment is not supported.", "Use matrix, pmatrix, bmatrix, vmatrix, cases or aligned.");
        var rows = new List<MathNode[]>();
        var cells = new List<MathNode>();
        while (true)
        {
            cells.Add(Single(Row(Stop.Cell))); // returns only at &, \\ or \end
            if (pos < tex.Length && tex[pos] == '&')
            {
                pos++;
                continue;
            }
            if (IsDoubleBackslash())
            {
                pos += 2;
                rows.Add(cells.ToArray());
                cells = [];
                continue;
            }
            var end = pos;
            pos += "\\end".Length;
            var closing = EnvironmentName();
            if (closing != name) throw new Failure("LF302", end, $"\\end{{{closing}}} does not match \\begin{{{name}}}.", $"Close it with \\end{{{name}}}.");
            break;
        }
        if (cells is not [Mrow { Children.Length: 0 }]) rows.Add(cells.ToArray()); // a trailing \\ leaves one empty cell
        var table = new Mtable(rows.ToArray(), name switch { "cases" => "left left", "aligned" => "right left", _ => null });
        return name switch
        {
            "pmatrix" => new Mrow([new Mo("(", true), table, new Mo(")", true)]),
            "bmatrix" => new Mrow([new Mo("[", true), table, new Mo("]", true)]),
            "vmatrix" => new Mrow([new Mo("|", true), table, new Mo("|", true)]),
            "cases" => new Mrow([new Mo("{", true), table]),
            _ => table
        };
    }

    private string EnvironmentName()
    {
        SkipSpaces();
        var at = pos;
        if (pos >= tex.Length || tex[pos] != '{') throw new Failure("LF302", at, "Environment names go in braces.", "Write \\begin{matrix} … \\end{matrix}.");
        var close = tex.IndexOf('}', pos);
        if (close < 0) throw new Failure("LF302", at, "An environment name is not closed.", "Add the closing brace.");
        var name = tex[(pos + 1)..close];
        pos = close + 1;
        return name;
    }

    private string TextArgument()
    {
        SkipSpaces();
        var at = pos;
        if (pos >= tex.Length || tex[pos] != '{') throw new Failure("LF302", at, "\\text needs its words in braces.", "Write \\text{like this}.");
        var text = new StringBuilder();
        var nesting = 0;
        for (pos++; pos < tex.Length; pos++)
        {
            var c = tex[pos];
            if (c == '\\' && pos + 1 < tex.Length && tex[pos + 1] is '{' or '}' or '\\' or '$' or '%' or '&' or '#' or '_')
            {
                text.Append(tex[++pos]);
                continue;
            }
            if (c == '{') nesting++;
            else if (c == '}' && nesting-- == 0)
            {
                pos++;
                return text.ToString();
            }
            text.Append(c);
        }
        throw new Failure("LF302", at, "\\text is not closed.", "Add the closing brace.");
    }

    // A null alphabet means \mathrm: keep the letters and render them upright.
    private static MathNode Styled(MathNode node, Func<string, string>? alphabet) => node switch
    {
        Mi mi => alphabet is null ? mi with { Normal = true } : new Mi(alphabet(mi.Text)),
        Mn mn => alphabet is null ? mn : new Mn(alphabet(mn.Text)),
        Mrow row => new Mrow(row.Children.Select(child => Styled(child, alphabet)).ToArray()),
        _ => node
    };

    private bool At(string text) => tex.AsSpan(pos).StartsWith(text, StringComparison.Ordinal)
        && !(char.IsAsciiLetter(text[^1]) && pos + text.Length < tex.Length && char.IsAsciiLetter(tex[pos + text.Length]));

    private bool IsWord(string name) => At("\\" + name);

    private bool IsDoubleBackslash() => At("\\\\");

    private void SkipSpaces()
    {
        while (pos < tex.Length && char.IsWhiteSpace(tex[pos])) pos++;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.TexParserTests"`
Expected: PASS. If an expected position in the theory differs by one column, fix the parser rather than the test. Positions are 1-based and point at the offending character: the `\` of a command, the end of the string for an unclosed group, or the second `_` for a double subscript.

- [ ] **Step 7: Commit**

```bash
git add src/LearnForge.Core tests/LearnForge.Tests/TexParserTests.cs
git commit -m "feat(core): parse the TeX subset into MathML-shaped nodes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Markdown subset compiler

**Files:**
- Modify: `src/LearnForge.Core/LearnForge.Core.csproj`
- Modify: `src/LearnForge.Core/Services/Content/ContentEngine.cs` (add `IsIdentifier`)
- Create: `src/LearnForge.Core/Services/Content/RichTextCompiler.cs`
- Test: `tests/LearnForge.Tests/RichTextCompilerTests.cs`

**Interfaces:**
- Consumes: AST (Task 1), `LanguageTag` (Task 2), `TexParser` (Task 3), `Diagnostic` with `Guidance` (Task 3).
- Produces: `new RichTextCompiler(List<Diagnostic> diagnostics)`; `.Blocks(string? text, string path, string? lessonId = null) : RichBlock[]`; `.Inlines(string? text, string path, string? lessonId = null, bool plainOnly = false) : RichInline[]`; `RichTextCompiler.CodeLanguage() : Regex` (public, `^[a-z0-9+#.-]{1,30}$`); constants `MaxFieldLength = 20_000`, `MaxNesting = 8`, `MaxListNesting = 3`, `MaxNodes = 200_000`; `ContentEngine.IsIdentifier(string?) : bool`. One compiler instance per pack: its node budget spans every field.

- [ ] **Step 1: Write the failing test**

```csharp
using Xunit;

namespace LearnForge.Tests;

public class RichTextCompilerTests
{
    private static (RichBlock[] Blocks, List<Diagnostic> Diagnostics) Blocks(string text, string? lesson = "intro")
    {
        var diagnostics = new List<Diagnostic>();
        return (new RichTextCompiler(diagnostics).Blocks(text, "f", lesson), diagnostics);
    }

    private static RichInline[] Clean(string text)
    {
        var (blocks, diagnostics) = Blocks(text);
        Assert.Empty(diagnostics);
        return Assert.IsType<ParagraphNode>(Assert.Single(blocks)).Inlines;
    }

    private static string Text(RichInline node) => Assert.IsType<TextNode>(node).Text;

    [Fact] public void Inline_formatting_code_and_math_map_to_nodes()
    {
        var inlines = Clean(@"A *set* is **distinct**: `x`, $x^2$ and \$5.");
        Assert.Collection(inlines,
            n => Assert.Equal("A ", Text(n)),
            n => Assert.Equal("set", Text(Assert.Single(Assert.IsType<EmphasisNode>(n).Inlines))),
            n => Assert.Equal(" is ", Text(n)),
            n => Assert.Equal("distinct", Text(Assert.Single(Assert.IsType<StrongNode>(n).Inlines))),
            n => Assert.Equal(": ", Text(n)),
            n => Assert.Equal("x", Assert.IsType<CodeNode>(n).Code),
            n => Assert.Equal(", ", Text(n)),
            n => Assert.IsType<Msup>(Assert.IsType<Mrow>(Assert.IsType<MathInlineNode>(n).Root).Children[0]),
            n => Assert.Equal(" and $5.", Text(n)));
    }

    [Fact] public void Currency_dollars_stay_text() =>
        Assert.Equal("It costs $5 and $10.", Text(Assert.Single(Clean("It costs $5 and $10."))));

    [Fact] public void Soft_breaks_become_spaces_including_windows_line_endings()
    {
        Assert.Equal("a b", Text(Assert.Single(Clean("a\r\nb"))));
        Assert.Collection(Clean("a  \nb"), n => Assert.Equal("a", Text(n)), n => Assert.IsType<BreakNode>(n), n => Assert.Equal("b", Text(n)));
    }

    [Fact] public void Lists_quotes_and_fenced_code_map_to_blocks()
    {
        var (blocks, diagnostics) = Blocks("- one\n\n  3. two\n\n> said\n\n```python\nprint(1)\n```");
        Assert.Empty(diagnostics);
        var list = Assert.IsType<ListNode>(blocks[0]);
        Assert.False(list.Ordered);
        var nested = Assert.IsType<ListNode>(Assert.Single(list.Items).Last());
        Assert.True(nested.Ordered);
        Assert.Equal(3, nested.Start);
        Assert.IsType<QuoteNode>(blocks[1]);
        Assert.Equal(new CodeBlockNode("python", "print(1)"), blocks[2]);
    }

    [Fact] public void Link_schemes_map_to_typed_links()
    {
        var inlines = Clean("[a](https://example.org/x) [b](term:union) [c](lesson:sets#b1) [d](#b2) [e](lang:fr) [f](lang:ar)");
        Assert.Equal("https://example.org/x", Assert.IsType<LinkNode>(inlines[0]).Href);
        Assert.Equal("union", Assert.IsType<TermNode>(inlines[2]).TermId);
        var lesson = Assert.IsType<LessonLinkNode>(inlines[4]);
        Assert.Equal("sets", lesson.LessonId);
        Assert.Equal("b1", lesson.BlockId);
        var local = Assert.IsType<LessonLinkNode>(inlines[6]);
        Assert.Equal("intro", local.LessonId);
        Assert.Equal("b2", local.BlockId);
        Assert.Equal(TextDirection.Ltr, Assert.IsType<LangNode>(inlines[8]).Direction);
        Assert.Equal(TextDirection.Rtl, Assert.IsType<LangNode>(inlines[10]).Direction);
    }

    [Fact] public void Entities_decode_to_text() => Assert.Equal("a & b", Text(Assert.Single(Clean("a &amp; b"))));

    [Theory]
    [InlineData("# Title", "A11Y_STRUCTURE")]
    [InlineData("Title\n=====", "A11Y_STRUCTURE")]
    [InlineData("a <br> b", "LF201")]
    [InlineData("<div>x</div>", "LF201")]
    [InlineData("![alt](x.png)", "LF201")]
    [InlineData("$$x$$", "LF201")]
    [InlineData("$$\nx\n$$", "LF201")]
    [InlineData("    code", "LF201")]
    [InlineData("---", "LF201")]
    [InlineData("```\nx\n```", "LF201")]
    [InlineData("<https://a.example>", "LF201")]
    [InlineData("[](https://a.example)", "A11Y_STRUCTURE")]
    [InlineData("[x](http://a.example)", "LF202")]
    [InlineData("[x](mailto:a@b.example)", "LF202")]
    [InlineData("[x](lesson:Bad!)", "LF202")]
    [InlineData("[x](lang:xx)", "A11Y_LANGUAGE")]
    [InlineData(@"$\foo$", "LF301")]
    public void Unsupported_markdown_is_reported(string text, string code)
    {
        var (_, diagnostics) = Blocks(text);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.StartsWith("f:", diagnostic.Path);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Guidance));
    }

    [Fact] public void Positions_point_at_the_construct()
    {
        Assert.Equal("f:3:1", Assert.Single(Blocks("ok\n\n# Heading").Diagnostics).Path);
        Assert.Equal("f:1:8", Assert.Single(Blocks(@"a b c $\foo$").Diagnostics).Path);
    }

    [Fact] public void Same_lesson_links_need_a_lesson() =>
        Assert.Equal("LF202", Assert.Single(Blocks("[b](#b1)", lesson: null).Diagnostics).Code);

    [Fact] public void Inline_fields_accept_one_paragraph_and_plain_fields_accept_text_only()
    {
        var diagnostics = new List<Diagnostic>();
        var compiler = new RichTextCompiler(diagnostics);
        Assert.Empty(compiler.Inlines("- a", "list"));
        Assert.Equal("a", Text(Assert.Single(compiler.Inlines("*a*", "plain", plainOnly: true))));
        Assert.Equal(new[] { "LF201", "LF201" }, diagnostics.Select(d => d.Code));
        Assert.Empty(compiler.Inlines(null, "empty"));
    }

    [Fact] public void Limits_are_enforced()
    {
        Assert.Equal("LF204", Assert.Single(Blocks(new string('a', RichTextCompiler.MaxFieldLength + 1)).Diagnostics).Code);
        Assert.Equal("LF204", Assert.Single(Blocks(string.Concat(Enumerable.Repeat("> ", 9)) + "x").Diagnostics).Code);
        Assert.Equal("LF204", Assert.Single(Blocks("- a\n  - b\n    - c\n      - d").Diagnostics).Code);
    }

    [Fact] public void The_node_budget_spans_every_field_of_a_pack()
    {
        var diagnostics = new List<Diagnostic>();
        var compiler = new RichTextCompiler(diagnostics);
        var field = string.Concat(Enumerable.Repeat("*a* ", 4_000)); // about 12,000 nodes
        for (var i = 0; i < 17; i++) compiler.Blocks(field, $"f{i}");
        Assert.Equal("LF204", Assert.Single(diagnostics).Code);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.RichTextCompilerTests"`
Expected: build FAIL, `RichTextCompiler` not found.

- [ ] **Step 3: Add Markdig**

Replace `src/LearnForge.Core/LearnForge.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <!-- Parses the Markdown subset; HTML output is never used. -->
    <PackageReference Include="Markdig" Version="1.4.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Expose the identifier rule**

In `ContentEngine.cs`, below the `Identifier()` regex declaration, add:

```csharp
    public static bool IsIdentifier(string? id) => !string.IsNullOrWhiteSpace(id) && Identifier().IsMatch(id);
```

and change the body of the local `Ids` function in `Validate` to use it:

```csharp
            foreach (var id in ids)
                if (!IsIdentifier(id) || !seen.Add(id)) Error(path, $"Invalid or duplicate ID '{id}'.");
```

- [ ] **Step 5: Implement the compiler**

`src/LearnForge.Core/Services/Content/RichTextCompiler.cs`:

```csharp
using System.Text.RegularExpressions;
using Markdig;
using Md = Markdig.Syntax;
using MdInlines = Markdig.Syntax.Inlines;
using MdMath = Markdig.Extensions.Mathematics;

namespace LearnForge.Core;

// Compiles LearnForge's Markdown subset into the typed AST. Markdig only parses; its HTML renderer is never
// used. Anything outside the allowlist becomes a diagnostic. Use one instance per pack: the node budget spans it.
public sealed partial class RichTextCompiler(List<Diagnostic> diagnostics)
{
    public const int MaxFieldLength = 20_000;
    public const int MaxNesting = 8;
    public const int MaxListNesting = 3;
    public const int MaxNodes = 200_000;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseMathematics().UsePreciseSourceLocation().Build();
    private int nodes;
    private string path = "";
    private string? lessonId;

    [GeneratedRegex(@"^[a-z0-9+#.-]{1,30}$")]
    public static partial Regex CodeLanguage();

    // Block content: paragraphs, lists, quotes, fenced code and inline content. lessonId enables #block links.
    public RichBlock[] Blocks(string? text, string path, string? lessonId = null) =>
        Begin(text, path, lessonId) ? MapBlocks(Markdown.Parse(text!, Pipeline), 0, 0) : [];

    // Inline content: exactly one paragraph. plainOnly limits it to text, for native <option> elements.
    public RichInline[] Inlines(string? text, string path, string? lessonId = null, bool plainOnly = false)
    {
        if (!Begin(text, path, lessonId)) return [];
        var blocks = Markdown.Parse(text!, Pipeline).Where(b => b is not Md.LinkReferenceDefinitionGroup).ToArray();
        if (blocks.Length == 0) return [];
        if (blocks.Length != 1 || blocks[0] is not Md.ParagraphBlock paragraph)
        {
            Report("LF201", blocks.Length > 1 ? blocks[1] : blocks[0], "This field accepts one paragraph of inline text.", "Remove lists, quotes, code fences and blank lines.");
            return [];
        }
        var inlines = MapInlines(paragraph.Inline, 0);
        if (plainOnly && inlines.Any(n => n is not TextNode))
        {
            Report("LF201", paragraph, "Dropdown options render in a native select and accept plain text only.", "Remove formatting, math and links from this option.");
            return [new TextNode(RichText.PlainText(inlines))];
        }
        return inlines;
    }

    private bool Begin(string? text, string fieldPath, string? lesson)
    {
        path = fieldPath;
        lessonId = lesson;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length <= MaxFieldLength) return true;
        diagnostics.Add(new("LF204", path, $"A rich-text field is limited to {MaxFieldLength:D} characters.", "Split the content into several blocks."));
        return false;
    }

    private RichBlock[] MapBlocks(Md.ContainerBlock container, int depth, int listDepth)
    {
        if (depth > MaxNesting)
        {
            Report("LF204", container, "Content is nested too deeply.", "Flatten nested lists and quotes.");
            return [];
        }
        var result = new List<RichBlock>();
        foreach (var block in container)
        {
            RichBlock? mapped = block switch
            {
                Md.LinkReferenceDefinitionGroup => null,
                Md.ParagraphBlock p => Count(new ParagraphNode(MapInlines(p.Inline, depth + 1))),
                Md.ListBlock list => MapList(list, depth, listDepth),
                Md.QuoteBlock quote => Count(new QuoteNode(MapBlocks(quote, depth + 1, listDepth))),
                MdMath.MathBlock math => RejectBlock(math, "LF201", "Display math ($$…$$) is not supported in text.", "Use a math block, which carries a plain-language description."),
                Md.FencedCodeBlock fence => MapFence(fence),
                Md.CodeBlock code => RejectBlock(code, "LF201", "Indented code is not supported.", "Use a fenced code block with a language label, e.g. ```python."),
                Md.HeadingBlock heading => RejectBlock(heading, "A11Y_STRUCTURE", "Headings are not supported inside content.", "Give the block a title, or split the content into several blocks."),
                Md.ThematicBreakBlock rule => RejectBlock(rule, "LF201", "Horizontal rules are not supported.", "Split the content into separate blocks instead."),
                Md.HtmlBlock html => RejectBlock(html, "LF201", "Raw HTML is not supported.", "Use Markdown formatting or a structured block."),
                _ => RejectBlock(block, "LF201", "This Markdown construct is not supported.", "See the Markdown subset in the authoring guide.")
            };
            if (mapped is not null) result.Add(mapped);
        }
        return result.ToArray();
    }

    private RichBlock? MapList(Md.ListBlock list, int depth, int listDepth)
    {
        if (listDepth >= MaxListNesting) return RejectBlock(list, "LF204", $"Lists can be nested at most {MaxListNesting} levels.", "Flatten the list.");
        var items = list.OfType<Md.ListItemBlock>().Select(item => MapBlocks(item, depth + 1, listDepth + 1)).ToArray();
        var start = list.IsOrdered && int.TryParse(list.OrderedStart, out var number) ? number : 1;
        return Count(new ListNode(list.IsOrdered, start, items));
    }

    private RichBlock? MapFence(Md.FencedCodeBlock fence)
    {
        var language = fence.Info?.Trim() ?? "";
        return CodeLanguage().IsMatch(language)
            ? Count(new CodeBlockNode(language, fence.Lines.ToString()))
            : RejectBlock(fence, "LF201", "Code fences need a language label.", "Write the language after the opening fence, e.g. ```python, or ```text for plain text.");
    }

    private RichInline[] MapInlines(MdInlines.ContainerInline? container, int depth)
    {
        if (container is null) return [];
        if (depth > MaxNesting)
        {
            Report("LF204", container, "Formatting is nested too deeply.", "Simplify nested emphasis and links.");
            return [];
        }
        var result = new List<RichInline>();
        foreach (var inline in container)
        {
            var mapped = inline switch
            {
                MdInlines.LiteralInline literal => new TextNode(literal.Content.ToString()),
                MdInlines.HtmlEntityInline entity => new TextNode(entity.Transcoded.ToString()),
                MdInlines.LineBreakInline lineBreak => lineBreak.IsHard ? new BreakNode() : new TextNode(" "),
                MdInlines.CodeInline code => new CodeNode(code.Content),
                MdMath.MathInline math => MapMath(math),
                MdInlines.EmphasisInline emphasis => MapEmphasis(emphasis, depth),
                MdInlines.LinkInline link => MapLink(link, depth),
                MdInlines.AutolinkInline auto => RejectInline(auto, "LF201", "Bare links are not supported.", "Write [descriptive text](https://…)."),
                MdInlines.HtmlInline html => RejectInline(html, "LF201", "Raw HTML is not supported.", "Use Markdown formatting instead."),
                _ => RejectInline(inline, "LF201", "This Markdown construct is not supported.", "See the Markdown subset in the authoring guide.")
            };
            if (mapped is null) continue;
            Count(mapped);
            if (mapped is TextNode text && result.Count > 0 && result[^1] is TextNode previous) result[^1] = new TextNode(previous.Text + text.Text);
            else result.Add(mapped);
        }
        return result.ToArray();
    }

    private RichInline? MapMath(MdMath.MathInline math)
    {
        if (math.DelimiterCount != 1) return RejectInline(math, "LF201", "Display math ($$…$$) is not supported in text.", "Use $…$ for inline math, or a math block with a description.");
        var tex = math.Content.ToString();
        return new MathInlineNode(tex, TexParser.Parse(tex, path, diagnostics, math.Line, math.Column + 1));
    }

    private RichInline? MapEmphasis(MdInlines.EmphasisInline emphasis, int depth)
    {
        var children = MapInlines(emphasis, depth + 1);
        return emphasis.DelimiterCount switch
        {
            1 => new EmphasisNode(children),
            2 => new StrongNode(children),
            _ => RejectInline(emphasis, "LF201", "This emphasis style is not supported.", "Use *emphasis* or **strong**.")
        };
    }

    private RichInline? MapLink(MdInlines.LinkInline link, int depth)
    {
        if (link.IsImage) return RejectInline(link, "LF201", "Images are not supported in text.", "Describe the content in words. Figure blocks with reviewed alternatives are planned.");
        var children = MapInlines(link, depth + 1);
        if (RichText.IsBlank(children)) return RejectInline(link, "A11Y_STRUCTURE", "Links need descriptive text.", "Write the text between the square brackets, e.g. [the union](term:union).");
        var url = link.Url ?? "";
        if (url.StartsWith("https://", StringComparison.Ordinal))
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                ? new LinkNode(url, children)
                : RejectInline(link, "LF202", "This link is not a valid HTTPS address.", "Check the address.");
        if (url.StartsWith("lesson:", StringComparison.Ordinal))
        {
            var target = url["lesson:".Length..].Split('#', 2);
            var block = target.Length == 2 ? target[1] : null;
            return ContentEngine.IsIdentifier(target[0]) && (block is null || ContentEngine.IsIdentifier(block))
                ? new LessonLinkNode(target[0], block, children)
                : RejectInline(link, "LF202", "Lesson links look like lesson:lesson-id or lesson:lesson-id#block-id.", "Use lowercase IDs.");
        }
        if (url.StartsWith('#'))
        {
            if (lessonId is null) return RejectInline(link, "LF202", "Same-lesson block links work only inside lessons.", "Use lesson:lesson-id#block-id.");
            return ContentEngine.IsIdentifier(url[1..])
                ? new LessonLinkNode(lessonId, url[1..], children)
                : RejectInline(link, "LF202", "Block links look like #block-id.", "Use the block's lowercase ID.");
        }
        if (url.StartsWith("term:", StringComparison.Ordinal))
            return ContentEngine.IsIdentifier(url[5..])
                ? new TermNode(url[5..], children)
                : RejectInline(link, "LF202", "Term links look like term:glossary-id.", "Use the glossary entry's lowercase ID.");
        if (url.StartsWith("lang:", StringComparison.Ordinal))
            return LanguageTag.IsValid(url[5..])
                ? new LangNode(url[5..], LanguageTag.DirectionOf(url[5..]), children)
                : RejectInline(link, "A11Y_LANGUAGE", $"'{url[5..]}' is not a valid language tag.", "Use a BCP 47 tag such as fr, pt-BR or zh-Hant.");
        return RejectInline(link, "LF202", url.StartsWith("http:", StringComparison.Ordinal) ? "Links must use HTTPS." : "This link type is not supported.",
            "Use https:, lesson:, #block, term: or lang: links.");
    }

    private T Count<T>(T node)
    {
        if (++nodes == MaxNodes + 1)
            diagnostics.Add(new("LF204", "pack", "The pack has more than 200,000 content nodes.", "Split the course into smaller packs."));
        return node;
    }

    private RichBlock? RejectBlock(Md.MarkdownObject at, string code, string message, string guidance)
    {
        Report(code, at, message, guidance);
        return null;
    }

    private RichInline? RejectInline(Md.MarkdownObject at, string code, string message, string guidance)
    {
        Report(code, at, message, guidance);
        return null;
    }

    private void Report(string code, Md.MarkdownObject at, string message, string guidance) =>
        diagnostics.Add(new(code, $"{path}:{at.Line + 1}:{at.Column + 1}", message, guidance));
}
```

If `mapped` in `MapInlines` does not infer a type because the arms differ, declare it as `RichInline? mapped = inline switch { … };`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.RichTextCompilerTests"`
Expected: PASS. Then run `dotnet test LearnForge.slnx` for the full suite.

- [ ] **Step 7: Commit**

```bash
git add src/LearnForge.Core tests/LearnForge.Tests/RichTextCompilerTests.cs
git commit -m "feat(core): compile the Markdown subset into the rich-text AST

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Reject duplicate properties; freeze schema 1 records and fixture

**Files:**
- Create: `src/LearnForge.Core/Services/Content/JsonDuplicates.cs`
- Modify: `src/LearnForge.Core/Services/Content/ContentEngine.cs` (`Compile`)
- Create: `src/LearnForge.Core/Domain/Legacy/V1Pack.cs`, `V1Lesson.cs`, `V1ContentBlock.cs`, `V1Question.cs`, `V1Option.cs`, `V1Slot.cs`, `V1Scenario.cs`
- Create: `tests/LearnForge.Tests/Fixtures/reasoning-foundations.v1.json` (generated), `tests/LearnForge.Tests/TestPacks.cs`
- Modify: `tests/LearnForge.Tests/LearnForge.Tests.csproj`
- Test: `tests/LearnForge.Tests/LegacyContentTests.cs`

**Interfaces:**
- Produces: `JsonDuplicates.Find(string source) : string?` (internal; the JSON path without `$`, e.g. `pack.lessons[1].blocks[0].text`). The `V1*` records, whose JSON matches schema 1 byte for byte. `TestPacks.PackFile(id)`, `TestPacks.Fixture(name)`, `TestPacks.V1() : V1Pack`, `TestPacks.Content(V1Pack) : StringContent`. `LegacyContentTests.V1LessonHashes` and `LegacyContentTests.EvidenceLessonHash` (pinned constants reused by later tasks).

- [ ] **Step 1: Generate the frozen fixture before any model change**

The current CLI writes the expanded v1 pack as `pack.private.json`, which is exactly a stored v1 release and a valid v1 source.

```bash
out="$(mktemp -d)/v1" && dotnet run --project tools/cli -- build packs/reasoning-foundations.json --out "$out" \
  && mkdir -p tests/LearnForge.Tests/Fixtures && cp "$out/pack.private.json" tests/LearnForge.Tests/Fixtures/reasoning-foundations.v1.json
```

Expected: `PASS reasoning-foundations@1.0.0: 3 lessons, 40 questions, 3 objectives.` and a new fixture file with `"schemaVersion": 1` and no `"templates"` key.

Add to `tests/LearnForge.Tests/LearnForge.Tests.csproj`, inside `<Project>`:

```xml
  <ItemGroup>
    <None Include="Fixtures/**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing test**

`tests/LearnForge.Tests/TestPacks.cs`:

```csharp
using System.Text;

namespace LearnForge.Tests;

// Source packs for tests. The v1 fixture is reasoning-foundations as it shipped before schema 2, with templates
// expanded; tests derive packs from it with `with` and publish them as schema 1 sources.
public static class TestPacks
{
    public static string PackFile(string packId) => Path.Combine(AppContext.BaseDirectory, "packs", packId + ".json");

    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static V1Pack V1() => Json.Read<V1Pack>(File.ReadAllText(Fixture("reasoning-foundations.v1.json")));

    public static StringContent Content(V1Pack pack) => new(Json.Write(pack), Encoding.UTF8, "application/json");
}
```

`tests/LearnForge.Tests/LegacyContentTests.cs`:

```csharp
using Xunit;

namespace LearnForge.Tests;

public class LegacyContentTests
{
    // ContentHash values of the bundled lessons before schema 2. If they change, every learner who completed
    // these lessons would see "Updated since you read it".
    public static readonly Dictionary<string, string> V1LessonHashes = new()
    {
        ["sets-intro"] = "dc18f5d956267fe200282aa77c2ac53acd2513e323d1c7e95bdc7cb1008592da",
        ["logic-intro"] = "ee2d583bf4f3ffc8ae9009bfb7d54ff6e3dd148c833f0bba12e973f5e704da59",
        ["ordering-intro"] = "b403f04660fefb540fb7001cae56fc3a125bee5d7a9d1ae22e97bdec0f0d7d2d"
    };

    public const string EvidenceLessonHash = "ab603ebeb1bbb96564b92711743156a1b88171e87d8393c1748bc214c84fd5fa";

    [Fact] public void Duplicate_properties_are_rejected_with_their_path()
    {
        var top = Assert.Single(ContentEngine.Compile("{\"schemaVersion\":1,\"id\":\"a\",\"id\":\"b\"}").Diagnostics);
        Assert.Equal("LF002", top.Code);
        Assert.Equal("id", top.Path);
        var nested = ContentEngine.Compile("{\"pack\":{\"lessons\":[{\"id\":\"x\"},{\"blocks\":[{\"kind\":\"text\",\"text\":\"a\",\"text\":\"b\"}]}]}}");
        Assert.Null(nested.Pack);
        Assert.Equal("pack.lessons[1].blocks[0].text", Assert.Single(nested.Diagnostics).Path);
    }

    [Fact] public void Malformed_json_is_left_to_the_parser() =>
        Assert.Equal("LF001", Assert.Single(ContentEngine.Compile("{\"a\":").Diagnostics).Code);

    [Fact] public void The_frozen_v1_fixture_keeps_pre_schema_2_lesson_hashes()
    {
        Assert.Equal(V1LessonHashes, TestPacks.V1().Lessons.ToDictionary(l => l.Id, l => ContentHash.Of(l)));
        var evidence = Json.Read<V1Pack>(File.ReadAllText(TestPacks.PackFile("evidence-lab")));
        Assert.Equal(EvidenceLessonHash, ContentHash.Of(Assert.Single(evidence.Lessons)));
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.LegacyContentTests"`
Expected: build FAIL, `V1Pack` not found.

- [ ] **Step 4: Create the schema 1 records**

Each file uses `namespace LearnForge.Core;`. The records mirror today's `Pack`, `Lesson`, `ContentBlock`, `Question`, `Option`, `Slot` and `Scenario` exactly (same property names and order), so their JSON and hashes are identical.

`Domain/Legacy/V1Pack.cs`:

```csharp
namespace LearnForge.Core;

// Schema 1 exactly as it shipped. Reads v1 sources, releases stored before format 2 and v1 attempt snapshots.
// Never change these records: stored data and lesson hashes depend on their exact shape.
public sealed record V1Pack(int SchemaVersion, string Id, string Version, string Title, string Description,
    string License, Objective[] Objectives, V1Lesson[] Lessons, V1Question[] Questions, V1Scenario[] Scenarios,
    Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy? Readiness = null,
    CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null);
```

```csharp
public sealed record V1Lesson(string Id, string Title, string Summary, string[] ObjectiveIds, V1ContentBlock[] Blocks);
```
```csharp
public sealed record V1ContentBlock(ContentBlockKind Kind, string Text, string? Title = null);
```
```csharp
public sealed record V1Question(string Id, string FamilyId, QuestionKind Kind, string Prompt, string[] ObjectiveIds,
    V1Option[] Options, V1Slot[] Slots, int SelectCount, bool Reuse, Grading Grading, string Explanation,
    string? ScenarioId = null, decimal Weight = 1);
```
```csharp
public sealed record V1Option(string Id, string Text);
```
```csharp
public sealed record V1Slot(string Id, string Text, V1Option[] Options);
```
```csharp
public sealed record V1Scenario(string Id, string Title, string Background);
```

- [ ] **Step 5: Implement duplicate detection**

`src/LearnForge.Core/Services/Content/JsonDuplicates.cs`:

```csharp
using System.Text;
using System.Text.Json;

namespace LearnForge.Core;

// JSON readers disagree about which duplicate property wins, so the compiler rejects duplicates outright.
internal static class JsonDuplicates
{
    private sealed class Scope(string path, bool isObject)
    {
        public string Path { get; } = path;
        public HashSet<string>? Names { get; } = isObject ? [] : null;
        public string? Property { get; set; }
        public int Index { get; set; }
    }

    // Returns the path of the first repeated property, or null. Malformed JSON is left for the parser to report.
    public static string? Find(string source)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(source), new JsonReaderOptions { MaxDepth = 32 });
        var scopes = new Stack<Scope>();
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var scope = scopes.Peek();
                        var name = reader.GetString()!;
                        if (!scope.Names!.Add(name)) return Join(scope.Path, name);
                        scope.Property = name;
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        scopes.Push(new(ValuePath(scopes), reader.TokenType == JsonTokenType.StartObject));
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        scopes.Pop();
                        break;
                    default:
                        ValuePath(scopes);
                        break;
                }
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    // The path of the value about to be read; advances the index of an enclosing array.
    private static string ValuePath(Stack<Scope> scopes)
    {
        if (scopes.Count == 0) return "";
        var parent = scopes.Peek();
        return parent.Names is null ? $"{parent.Path}[{parent.Index++}]" : Join(parent.Path, parent.Property!);
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : path + "." + name;
}
```

In `ContentEngine.Compile`, directly after the 2 MB check, add:

```csharp
            if (JsonDuplicates.Find(source) is { } duplicate)
                return new(null, [new("LF002", duplicate, "This property appears more than once.",
                    "Remove the repeated property. JSON readers disagree about which value wins.")], hash);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.LegacyContentTests"`
Expected: PASS. Then run `dotnet test LearnForge.slnx` and `make check-content`; both bundled packs must still compile.

- [ ] **Step 7: Commit**

```bash
git add src/LearnForge.Core tests/LearnForge.Tests
git commit -m "feat(core): reject duplicate JSON properties and freeze the schema 1 records

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The release model carries the AST

This is the one atomic model switch: Core, the API, the CLI and existing tests change together so the build stays green. Only schema 1 sources are accepted until Task 8.

**Files:**
- Create: `src/LearnForge.Core/Domain/Enums/CourseLevel.cs`
- Create: `src/LearnForge.Core/Domain/Content/Module.cs`, `GlossaryEntry.cs`, `CatalogMetadata.cs`, `InlineCheckKey.cs`
- Create: `src/LearnForge.Core/Domain/Content/Blocks/LessonBlock.cs`, `TextBlock.cs`, `CalloutBlock.cs`, `ExampleBlock.cs`, `CodeSampleBlock.cs`
- Modify: `src/LearnForge.Core/Domain/Content/Pack.cs`, `Lesson.cs`, `Question.cs`, `Option.cs`, `Slot.cs`, `Scenario.cs`, `DeliveryQuestion.cs`
- Modify: `src/LearnForge.Core/Domain/Assessment/Grade.cs`, `src/LearnForge.Core/Services/Assessment/Grader.cs`
- Delete: `src/LearnForge.Core/Domain/Content/ContentBlock.cs`
- Move: `src/LearnForge.Core/Domain/Enums/ContentBlockKind.cs` → `src/LearnForge.Core/Domain/Legacy/ContentBlockKind.cs`
- Create: `src/LearnForge.Core/Services/Content/V1Upcaster.cs`, `ReleaseReader.cs`
- Modify: `src/LearnForge.Core/Services/Content/ContentEngine.cs`, `src/LearnForge.Core/Domain/Rich/RichText.cs`
- Create: `apps/api/Contracts/Attempts/FeedbackDto.cs`
- Modify: `apps/api/Contracts/Attempts/AttemptSnapshot.cs`, `AttemptView.cs`, `apps/api/Services/Attempts/AttemptService.cs`, `apps/api/Services/Content/ReleaseCache.cs`
- Modify: `tools/cli/Program.cs`
- Modify tests: `CoreTests.cs`, `GoalPolicyTests.cs`, `LearningRecordTests.cs`, `ApiTests.cs`, `LegacyContentTests.cs`

**Interfaces:**
- Consumes: AST (Task 1), `V1*` records and fixture (Task 5).
- Produces (later tasks rely on these exact shapes):
  - `Pack(int Format, int SchemaVersion, string Id, string Version, string Title, string Description, string License, string? Language, TextDirection Direction, CatalogMetadata? Catalog, Objective[] Objectives, Module[] Modules, GlossaryEntry[] Glossary, Lesson[] Lessons, Question[] Questions, Scenario[] Scenarios, Blueprint[] Blueprints, SourceReference[] Sources, InlineCheckKey[] Checks, Dictionary<string, string> LessonHashes, ReadinessPolicy? Readiness = null, CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null)` with `const int CurrentFormat = 2`.
  - `Lesson(string Id, string Title, string Summary, string[] ObjectiveIds, LessonBlock[] Blocks, int? Minutes = null, string? Language = null, TextDirection? Direction = null)`.
  - `Question(…, RichBlock[] Prompt, …, RichBlock[] Explanation, …)`, `Option(string Id, RichInline[] Text)`, `Slot(string Id, RichInline[] Text, Option[] Options)`, `Scenario(string Id, string Title, RichBlock[] Background)`, `DeliveryQuestion(…, RichBlock[] Prompt, …)`.
  - `LessonBlock` (abstract `Id`), `TextBlock/CalloutBlock/ExampleBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Body)`, `CodeSampleBlock(string Id, string? Title, string? Language, string Code)`.
  - `Module(string Id, string Title, string[] LessonIds)`, `GlossaryEntry(string Id, string Term, RichInline[] Definition)`, `CatalogMetadata(string Subject, CourseLevel Level, string[] Tags, decimal? EstimatedHours)`, `CourseLevel { Introductory, Intermediate, Advanced }`, `InlineCheckKey(string LessonId, string BlockId, Grading Grading, RichBlock[] Explanation)`.
  - `Grade(decimal Earned, decimal Possible, bool FullyCorrect, string[] Correct, Dictionary<string, string>? Matches)`.
  - `V1Upcaster.ToRelease(V1Pack, List<Diagnostic>? = null) : Pack`, `V1Upcaster.ToQuestion(V1Question) : Question`, `V1Upcaster.ToScenario(V1Scenario) : Scenario`.
  - `ReleaseReader.Read(string json) : Pack`.
  - `ContentEngine.CheckIds(List<Diagnostic>, string path, IEnumerable<string>)` and `ContentEngine.CheckQuestion(Question, string path, List<Diagnostic>)` (both `internal`).
  - `RichText.PlainText(LessonBlock) : string`.
  - API: `FeedbackDto(Grade Grade, RichBlock[] Explanation)`; `AttemptView.Feedback : Dictionary<string, FeedbackDto>`, `AttemptView.Results : Dictionary<string, FeedbackDto>?`; `AttemptSnapshot(…, ReadinessPolicy Readiness, int ContentSchema)`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/LearnForge.Tests/LegacyContentTests.cs`, inside the class:

```csharp
    [Fact] public void V1_sources_compile_to_literal_rich_text()
    {
        var source = TestPacks.V1();
        var literal = source with { Lessons = [source.Lessons[0] with { Blocks = [new(ContentBlockKind.Text, "*not emphasis* costs $5 <b>")] }, .. source.Lessons[1..]] };
        var compiled = ContentEngine.Compile(Json.Write(literal));
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var block = Assert.IsType<TextBlock>(Assert.Single(compiled.Pack!.Lessons[0].Blocks));
        Assert.Equal("b1", block.Id);
        var paragraph = Assert.IsType<ParagraphNode>(Assert.Single(block.Body));
        Assert.Equal("*not emphasis* costs $5 <b>", Assert.IsType<TextNode>(Assert.Single(paragraph.Inlines)).Text);
    }

    [Fact] public void V1_packs_republished_after_the_upgrade_keep_their_lesson_hashes()
    {
        var compiled = ContentEngine.Compile(Json.Write(TestPacks.V1())).Pack!;
        Assert.Equal(Pack.CurrentFormat, compiled.Format);
        Assert.Equal(1, compiled.SchemaVersion);
        Assert.Equal(V1LessonHashes, compiled.LessonHashes);
        Assert.Equal(new[] { "b1", "b2", "b3" }, compiled.Lessons[0].Blocks.Select(b => b.Id));
        Assert.Null(Assert.IsType<CodeSampleBlock>(compiled.Lessons[2].Blocks[2]).Language);
    }

    [Fact] public void Stored_releases_of_both_formats_read_back()
    {
        var legacy = ReleaseReader.Read(File.ReadAllText(TestPacks.Fixture("reasoning-foundations.v1.json")));
        Assert.Equal(V1LessonHashes, legacy.LessonHashes);
        Assert.Equal("Think in sets", legacy.Lessons[0].Title);
        var current = ContentEngine.Compile(File.ReadAllText(TestPacks.PackFile("evidence-lab"))).Pack!;
        var roundTrip = ReleaseReader.Read(Json.Write(current));
        Assert.Equal(Json.Write(current), Json.Write(roundTrip));
        Assert.Equal(EvidenceLessonHash, roundTrip.LessonHashes["evidence-first"]);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.LegacyContentTests"`
Expected: build FAIL (`TextBlock`, `ReleaseReader`, `Pack.Format` do not exist).

- [ ] **Step 3: Create the new release records**

`Domain/Enums/CourseLevel.cs`:

```csharp
namespace LearnForge.Core;

public enum CourseLevel { Introductory, Intermediate, Advanced }
```

`Domain/Content/Module.cs`:

```csharp
namespace LearnForge.Core;

// Modules group lessons; their order is the reading order.
public sealed record Module(string Id, string Title, string[] LessonIds);
```

`Domain/Content/GlossaryEntry.cs`:

```csharp
namespace LearnForge.Core;

public sealed record GlossaryEntry(string Id, string Term, RichInline[] Definition);
```

`Domain/Content/CatalogMetadata.cs`:

```csharp
namespace LearnForge.Core;

public sealed record CatalogMetadata(string Subject, CourseLevel Level, string[] Tags, decimal? EstimatedHours);
```

`Domain/Content/InlineCheckKey.cs`:

```csharp
namespace LearnForge.Core;

// The private half of an inline check. It lives in Pack.Checks, never in a lesson, so lessons are always safe to deliver.
public sealed record InlineCheckKey(string LessonId, string BlockId, Grading Grading, RichBlock[] Explanation);
```

`Domain/Content/Blocks/LessonBlock.cs`:

```csharp
using System.Text.Json.Serialization;

namespace LearnForge.Core;

// One block of a lesson. Blocks carry only learner-facing data: inline-check keys live in Pack.Checks.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(CalloutBlock), "callout")]
[JsonDerivedType(typeof(ExampleBlock), "example")]
[JsonDerivedType(typeof(CodeSampleBlock), "code")]
public abstract record LessonBlock
{
    public abstract string Id { get; init; }
}
```

`Domain/Content/Blocks/TextBlock.cs`, `CalloutBlock.cs`, `ExampleBlock.cs` (same shape, different name):

```csharp
namespace LearnForge.Core;

// Language overrides the lesson's language; the compiler derives Direction from it.
public sealed record TextBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Body) : LessonBlock;
```
```csharp
namespace LearnForge.Core;

public sealed record CalloutBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Body) : LessonBlock;
```
```csharp
namespace LearnForge.Core;

public sealed record ExampleBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Body) : LessonBlock;
```

`Domain/Content/Blocks/CodeSampleBlock.cs`:

```csharp
namespace LearnForge.Core;

// Language is the programming-language label (required from schema 2). Code blocks take no human-language override.
public sealed record CodeSampleBlock(string Id, string? Title, string? Language, string Code) : LessonBlock;
```

- [ ] **Step 4: Change the existing release records**

Replace each file's record (keep `namespace LearnForge.Core;`):

`Pack.cs`:

```csharp
// A compiled, immutable release. Format marks the stored shape: 2 means rich-text AST plus precomputed lesson
// hashes. Releases stored before format 2 have no format property and are read through ReleaseReader.
// SchemaVersion is the source schema. Readiness, goal and mastery are optional in source JSON; packs that omit
// them keep the exam-readiness goal.
public sealed record Pack(int Format, int SchemaVersion, string Id, string Version, string Title, string Description,
    string License, string? Language, TextDirection Direction, CatalogMetadata? Catalog, Objective[] Objectives,
    Module[] Modules, GlossaryEntry[] Glossary, Lesson[] Lessons, Question[] Questions, Scenario[] Scenarios,
    Blueprint[] Blueprints, SourceReference[] Sources, InlineCheckKey[] Checks, Dictionary<string, string> LessonHashes,
    ReadinessPolicy? Readiness = null, CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null)
{
    public const int CurrentFormat = 2;
}
```

`Lesson.cs`:

```csharp
public sealed record Lesson(string Id, string Title, string Summary, string[] ObjectiveIds, LessonBlock[] Blocks,
    int? Minutes = null, string? Language = null, TextDirection? Direction = null);
```

`Question.cs`:

```csharp
public sealed record Question(string Id, string FamilyId, QuestionKind Kind, RichBlock[] Prompt, string[] ObjectiveIds,
    Option[] Options, Slot[] Slots, int SelectCount, bool Reuse, Grading Grading, RichBlock[] Explanation,
    string? ScenarioId = null, decimal Weight = 1);
```

`Option.cs`: `public sealed record Option(string Id, RichInline[] Text);`
`Slot.cs`: `public sealed record Slot(string Id, RichInline[] Text, Option[] Options);`
`Scenario.cs`: `public sealed record Scenario(string Id, string Title, RichBlock[] Background);`

`DeliveryQuestion.cs`:

```csharp
// Explicit allowlist for learner-facing delivery. Private grading data is never serialized here.
public sealed record DeliveryQuestion(string Id, QuestionKind Kind, RichBlock[] Prompt, string[] ObjectiveIds,
    Option[] Options, Slot[] Slots, int SelectCount, bool Reuse, string? ScenarioId, decimal Weight)
{
    public static DeliveryQuestion From(Question q) => new(q.Id, q.Kind, q.Prompt, q.ObjectiveIds,
        q.Options, q.Slots, q.SelectCount, q.Reuse, q.ScenarioId, q.Weight);
}
```

`Domain/Assessment/Grade.cs`:

```csharp
// Explanations are content, not grades: views attach them from the question.
public sealed record Grade(decimal Earned, decimal Possible, bool FullyCorrect, string[] Correct,
    Dictionary<string, string>? Matches);
```

In `Grader.Score`, change the last line to:

```csharp
        return new(fraction * q.Weight, q.Weight, full, q.Grading.Correct, q.Grading.Matches);
```

Then:

```bash
git rm src/LearnForge.Core/Domain/Content/ContentBlock.cs
git mv src/LearnForge.Core/Domain/Enums/ContentBlockKind.cs src/LearnForge.Core/Domain/Legacy/ContentBlockKind.cs
```

- [ ] **Step 5: Add the upcaster, reader and plain text for lesson blocks**

`Services/Content/V1Upcaster.cs`:

```csharp
namespace LearnForge.Core;

// Converts schema 1 content to the current release model. v1 text is literal: it is wrapped in text nodes and
// never interpreted as Markdown. Lesson hashes are computed on the v1 records, exactly as before schema 2,
// so republishing a v1 pack does not flag completed lessons as revised.
public static class V1Upcaster
{
    public static Pack ToRelease(V1Pack p, List<Diagnostic>? diagnostics = null) => new(Pack.CurrentFormat, p.SchemaVersion,
        p.Id, p.Version, p.Title, p.Description, p.License, null, TextDirection.Ltr, null, p.Objectives, [], [],
        p.Lessons.Select(l => ToLesson(l, diagnostics)).ToArray(), p.Questions.Select(ToQuestion).ToArray(),
        p.Scenarios.Select(ToScenario).ToArray(), p.Blueprints, p.Sources, [],
        p.Lessons.DistinctBy(l => l.Id).ToDictionary(l => l.Id, l => ContentHash.Of(l)), p.Readiness, p.Goal, p.Mastery);

    public static Question ToQuestion(V1Question q) => new(q.Id, q.FamilyId, q.Kind, RichText.Literal(q.Prompt), q.ObjectiveIds,
        q.Options.Select(ToOption).ToArray(), q.Slots.Select(ToSlot).ToArray(), q.SelectCount, q.Reuse, q.Grading,
        RichText.Literal(q.Explanation), q.ScenarioId, q.Weight);

    public static Scenario ToScenario(V1Scenario s) => new(s.Id, s.Title, RichText.Literal(s.Background));

    private static Option ToOption(V1Option o) => new(o.Id, RichText.LiteralInline(o.Text));

    private static Slot ToSlot(V1Slot s) => new(s.Id, RichText.LiteralInline(s.Text), s.Options.Select(ToOption).ToArray());

    private static Lesson ToLesson(V1Lesson l, List<Diagnostic>? diagnostics) =>
        new(l.Id, l.Title, l.Summary, l.ObjectiveIds, l.Blocks.Select((b, i) => ToBlock(l, b, "b" + (i + 1), diagnostics)).ToArray());

    // v1 blocks had no IDs. Positional IDs stay stable only while the block order is unchanged.
    private static LessonBlock ToBlock(V1Lesson lesson, V1ContentBlock b, string id, List<Diagnostic>? diagnostics)
    {
        if (!Enum.IsDefined(b.Kind)) diagnostics?.Add(new("LF100", lesson.Id, "Unsupported block. Use text, callout, example or code."));
        return b.Kind switch
        {
            ContentBlockKind.Callout => new CalloutBlock(id, b.Title, null, null, RichText.Literal(b.Text)),
            ContentBlockKind.Example => new ExampleBlock(id, b.Title, null, null, RichText.Literal(b.Text)),
            ContentBlockKind.Code => new CodeSampleBlock(id, b.Title, null, b.Text),
            _ => new TextBlock(id, b.Title, null, null, RichText.Literal(b.Text))
        };
    }
}
```

`Services/Content/ReleaseReader.cs`:

```csharp
using System.Text.Json;

namespace LearnForge.Core;

// Reads a stored release. Releases stored before format 2 hold schema 1 records and are upcast on read;
// stored data is never rewritten.
public static class ReleaseReader
{
    public static Pack Read(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = Json.Options.MaxDepth });
        var root = document.RootElement;
        return root.TryGetProperty("format", out _)
            ? root.Deserialize<Pack>(Json.Options) ?? throw new JsonException("Empty release.")
            : V1Upcaster.ToRelease(root.Deserialize<V1Pack>(Json.Options) ?? throw new JsonException("Empty release."));
    }
}
```

Append to `Domain/Rich/RichText.cs`, inside the class:

```csharp
    // Search text for one lesson block. Task 9 extends this switch with the other block kinds.
    public static string PlainText(LessonBlock block) => block switch
    {
        TextBlock b => PlainText(b.Body),
        CalloutBlock b => PlainText(b.Body),
        ExampleBlock b => PlainText(b.Body),
        CodeSampleBlock b => b.Code,
        _ => ""
    };
```

- [ ] **Step 6: Route compilation through the upcaster and share the question contract**

In `ContentEngine.cs`, keep `Expand`, `Variable()`, `Identifier()` and `IsIdentifier` unchanged. Replace `Compile` and `Validate` with the following, and add `CheckIds` and `CheckQuestion`:

```csharp
    public static Compilation Compile(string source)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        try
        {
            if (Encoding.UTF8.GetByteCount(source) > MaxSourceBytes) throw new InvalidOperationException("Pack source exceeds 2 MB.");
            if (JsonDuplicates.Find(source) is { } duplicate)
                return new(null, [new("LF002", duplicate, "This property appears more than once.",
                    "Remove the repeated property. JSON readers disagree about which value wins.")], hash);
            var root = JsonNode.Parse(source, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject
                ?? throw new InvalidOperationException("A pack must be a JSON object.");
            var templates = root["templates"] as JsonObject ?? new JsonObject();
            var node = Expand(root["pack"] ?? root, templates, new JsonObject(), 0, new ExpansionBudget());
            var schema = node is JsonObject body && body["schemaVersion"] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;
            if (schema != 1) return new(null, [new("LF100", "schemaVersion", "Only schema version 1 is supported.")], hash);
            var diagnostics = new List<Diagnostic>();
            var release = V1Upcaster.ToRelease(node!.Deserialize<V1Pack>(Json.Options) ?? throw new JsonException("Missing pack."), diagnostics);
            diagnostics.AddRange(Validate(release));
            return new(release, diagnostics.ToArray(), hash);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or NullReferenceException or NotSupportedException)
        {
            return new(null, [new("LF001", "$", e is JsonException j ? $"{j.Message}" : e.Message)], hash);
        }
    }

    public static Diagnostic[] Validate(Pack p)
    {
        var errors = new List<Diagnostic>();
        void Error(string path, string message) => errors.Add(new("LF100", path, message));
        if (p.SchemaVersion != 1) Error("schemaVersion", "Only schema version 1 is supported.");
        CheckIds(errors, "id", [p.Id]);
        if (!Version.TryParse(p.Version, out _)) Error("version", "Use a numeric release version, e.g. 1.0.0.");
        if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.License)) Error("title", "Title and license are required.");
        if (p.Objectives.Length is < 1 or > 200 || p.Questions.Length is < 1 or > 2000 || p.Lessons.Length is < 1 or > 500)
            Error("pack", "Require 1–200 objectives, 1–2000 questions and 1–500 lessons.");
        CheckIds(errors, "objectives", p.Objectives.Select(o => o.Id));
        CheckIds(errors, "lessons", p.Lessons.Select(l => l.Id));
        CheckIds(errors, "questions", p.Questions.Select(q => q.Id));
        CheckIds(errors, "scenarios", p.Scenarios.Select(s => s.Id));
        CheckIds(errors, "blueprints", p.Blueprints.Select(b => b.Id));
        var objectiveIds = p.Objectives.Select(o => o.Id).ToHashSet();
        void References(string path, string[] ids)
        {
            if (ids.Length == 0 || ids.Distinct().Count() != ids.Length || ids.Any(id => !objectiveIds.Contains(id))) Error(path, "Objectives must be nonempty, distinct, and resolve in this pack.");
        }
        foreach (var objective in p.Objectives)
        {
            if (objective.Prerequisites.Any(id => !objectiveIds.Contains(id))) Error(objective.Id, "Unknown prerequisite.");
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();
            bool Cycle(string id)
            {
                if (visited.Contains(id)) return false;
                if (!visiting.Add(id)) return true;
                var found = p.Objectives.FirstOrDefault(o => o.Id == id);
                var result = found?.Prerequisites.Any(Cycle) == true;
                visiting.Remove(id); visited.Add(id); return result;
            }
            if (Cycle(objective.Id)) Error(objective.Id, "Prerequisite cycle.");
            if (!p.Lessons.Any(l => l.ObjectiveIds.Contains(objective.Id))) Error(objective.Id, "Objective has no teaching lesson.");
            if (!p.Questions.Any(q => q.ObjectiveIds.Contains(objective.Id))) Error(objective.Id, "Objective has no practice.");
        }
        foreach (var l in p.Lessons)
        {
            References(l.Id, l.ObjectiveIds);
            if (string.IsNullOrWhiteSpace(l.Title) || l.Blocks.Length == 0) Error(l.Id, "A lesson requires a title and blocks.");
        }
        foreach (var q in p.Questions)
        {
            References(q.Id, q.ObjectiveIds);
            if (RichText.IsBlank(q.Prompt) || RichText.IsBlank(q.Explanation) || string.IsNullOrWhiteSpace(q.FamilyId)) Error(q.Id, "Prompt, explanation and family ID are required.");
            if (q.Weight <= 0 || q.Weight > 100) Error(q.Id, "Weight must be within (0, 100].");
            if (q.ScenarioId is { } sid && !p.Scenarios.Any(s => s.Id == sid)) Error(q.Id, "Unknown scenario.");
            CheckQuestion(q, q.Id, errors);
        }
        foreach (var source in p.Sources)
            if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https") Error("sources", "Reference URLs must use HTTPS.");
        foreach (var b in p.Blueprints)
        {
            References(b.Id, b.ObjectiveIds);
            if (b.Size is not (AssessmentSize.Short or AssessmentSize.Full) || b.Count < 1 || b.Count > 100 || b.Minutes is < 1 or > 600) Error(b.Id, "Invalid size/count/duration.");
            if (errors.Count == 0)
                try { ExamComposer.Compose(p, b, [], "validation"); }
                catch (InvalidOperationException ex) { Error(b.Id, ex.Message); }
        }
        if (p.Blueprints.Length == 0) Error("blueprints", "At least one blueprint is required.");
        var r = p.Readiness ?? new();
        if (r.ShortAttempts is < 1 or > 20 || r.FullAttempts is < 1 or > 20 || r.Threshold is < 0 or >= 100 || r.LookbackDays is < 1 or > 365 || r.MinimumFreshPercent is < 0 or > 100) Error("readiness", "Readiness policy is out of bounds.");
        if (!Enum.IsDefined(p.Goal)) Error("goal", "Goal must be readiness, mastery or completion.");
        var m = p.Mastery ?? new();
        if (m.MinimumEvidence < 1 || m.Window < m.MinimumEvidence || m.Window > 20 || m.ProficientPercent is < 50 or > 100 || m.ReviewAfterDays is < 1 or > 365)
            Error("mastery", "Mastery policy is out of bounds: 1 ≤ minimumEvidence ≤ window ≤ 20, proficientPercent 50–100, reviewAfterDays 1–365.");
        else if (p.Goal == CourseGoal.Mastery)
            // Like blueprint feasibility: an objective with too few families could never become proficient.
            foreach (var objective in p.Objectives)
                if (p.Questions.Where(q => q.ObjectiveIds.Contains(objective.Id)).Select(q => q.FamilyId).Distinct().Count() < m.MinimumEvidence)
                    Error(objective.Id, $"A mastery goal needs at least {m.MinimumEvidence} question families for this objective.");
        return errors.ToArray();
    }

    internal static void CheckIds(List<Diagnostic> errors, string path, IEnumerable<string> ids)
    {
        var seen = new HashSet<string>();
        foreach (var id in ids)
            if (!IsIdentifier(id) || !seen.Add(id)) errors.Add(new("LF100", path, $"Invalid or duplicate ID '{id}'."));
    }

    // The option, slot and key contract shared by bank questions and inline checks.
    internal static void CheckQuestion(Question q, string path, List<Diagnostic> errors)
    {
        void Error(string message) => errors.Add(new("LF100", path, message));
        CheckIds(errors, path + ".options", q.Options.Select(o => o.Id));
        CheckIds(errors, path + ".slots", q.Slots.Select(s => s.Id));
        var options = q.Options.Select(o => o.Id).ToHashSet();
        if (q.Grading.Policy is not (ScoringPolicy.Exact or ScoringPolicy.Partial)) Error("Scoring policy must be exact or partial.");
        if (q.Kind is QuestionKind.Single or QuestionKind.Multiple or QuestionKind.Sequence)
        {
            if (q.Options.Length < 2 || q.Grading.Correct.Distinct().Count() != q.Grading.Correct.Length || q.Grading.Correct.Any(id => !options.Contains(id))) Error("Invalid option/key contract.");
            if (q.Kind == QuestionKind.Single && (q.SelectCount != 1 || q.Grading.Correct.Length != 1)) Error("Single choice needs one key and one selection.");
            if (q.Kind == QuestionKind.Multiple && (q.SelectCount < 2 || q.SelectCount >= q.Options.Length || q.SelectCount != q.Grading.Correct.Length)) Error("Select-N requires N keys and at least one distractor.");
            if (q.Kind == QuestionKind.Sequence && !options.SetEquals(q.Grading.Correct)) Error("Sequence key must be a complete permutation.");
            if (q.Slots.Length > 0 || q.Grading.Matches is { Count: > 0 }) Error("Choice and sequence questions cannot contain slots/matches.");
        }
        else if (q.Kind is QuestionKind.Matching or QuestionKind.Dropdown)
        {
            if (q.Slots.Length == 0 || q.Grading.Correct.Length > 0 || q.Grading.Matches is null || !q.Slots.Select(s => s.Id).ToHashSet().SetEquals(q.Grading.Matches.Keys)) Error("Every slot requires exactly one match key.");
            foreach (var slot in q.Slots)
            {
                CheckIds(errors, path + "." + slot.Id, slot.Options.Select(o => o.Id));
                var bank = q.Kind == QuestionKind.Matching ? q.Options : slot.Options;
                if (bank.Length < 2 || !bank.Any(o => o.Id == q.Grading.Matches?.GetValueOrDefault(slot.Id))) Error("A slot key does not resolve in its option bank.");
            }
            if (q.Kind == QuestionKind.Matching && !q.Reuse && q.Grading.Matches is { } keys && keys.Values.Distinct().Count() != keys.Count) Error("Matching key reuses a token while reuse is disabled.");
        }
        else Error($"Unsupported question kind '{q.Kind}'.");
    }
```

`NotSupportedException` is new in the catch filter: System.Text.Json throws it when a polymorphic object has no `kind`.

- [ ] **Step 7: Update the API**

`apps/api/Contracts/Attempts/FeedbackDto.cs`:

```csharp
namespace LearnForge.Api.Contracts.Attempts;

// A released grade with its explanation, taken from the attempt's snapshot.
public sealed record FeedbackDto(Grade Grade, RichBlock[] Explanation);
```

`apps/api/Contracts/Attempts/AttemptSnapshot.cs`:

```csharp
namespace LearnForge.Api.Contracts.Attempts;

// Stored with each attempt. ContentSchema 2 marks rich-text questions; snapshots saved before it have no
// contentSchema property and are upcast from schema 1 on read.
public sealed record AttemptSnapshot(string Title, string Version, Objective[] Objectives, Scenario[] Scenarios,
    Blueprint Blueprint, Question[] Questions, ReadinessPolicy Readiness, int ContentSchema);
```

In `AttemptView.cs`, change the two grade dictionaries:

```csharp
    Dictionary<string, FeedbackDto> Feedback,
    DateTime ServerTime,
    Dictionary<string, FeedbackDto>? Results,
```

In `AttemptService.Start`, pass the schema:

```csharp
        var snapshot = new AttemptSnapshot(pack.Title, pack.Version, pack.Objectives, pack.Scenarios, blueprint, chosen, pack.Readiness ?? new(), Pack.CurrentFormat);
```

Replace `AttemptService.View` with:

```csharp
    public AttemptView View(Attempt a)
    {
        var s = Snapshot(a); var answers = Answers(a);
        var explanations = s.Questions.ToDictionary(q => q.Id, q => q.Explanation);
        var complete = a.Status == AttemptStatus.Completed;
        var results = complete ? s.Questions.ToDictionary(q => q.Id, q => new FeedbackDto(Grader.Score(q, answers.GetValueOrDefault(q.Id)), q.Explanation)) : null;
        var summary = complete ? new AttemptResultSummary(a.Earned, a.Possible,
            a.Possible == 0 ? 0 : a.Earned * 100 / a.Possible, a.CorrectPercent, a.Eligible, a.FreshPercent) : null;
        var feedback = Json.Read<Dictionary<string, Grade>>(a.FeedbackJson).ToDictionary(f => f.Key, f => new FeedbackDto(f.Value, explanations[f.Key]));
        return new AttemptView(a.Id, a.PackId, s.Title, s.Version, a.Mode, a.Size, a.Status, a.Revision,
            a.StartedAt, a.Deadline, a.CompletedAt, a.SectionIndex, Sections(a),
            s.Blueprint.LockSections && a.Mode == AssessmentMode.Mock, a.TimedOut, a.Focus,
            s.Questions.Select(DeliveryQuestion.From).ToArray(), s.Scenarios, s.Objectives, answers,
            feedback, Now, results, summary);
    }
```

In `ReleaseCache.Load`, replace the two lines that read and wrap the release with:

```csharp
            var pack = ReleaseReader.Read(release.ContentJson);
            return new ReleaseView(release.Id, pack, pack.LessonHashes);
```

- [ ] **Step 8: Update the CLI**

In `tools/cli/Program.cs`, the `init` starter stays schema 1 until Task 10. Replace the two constructor calls with the `V1` records:

```csharp
        var q = new V1Question("starter-q", "starter-family", QuestionKind.Single, "Which value is even?", ["parity"],
            [new("a", "2"), new("b", "3")], [], 1, false, new(ScoringPolicy.Exact, ["a"]), "An even integer is divisible by two.");
        var pack = new V1Pack(1, "my-course", "1.0.0", "My first course", "Replace this sample with your subject.", "Private",
            [new("parity", "Recognize even integers", [])],
            [new("introduction", "Even integers", "A short introduction.", ["parity"], [new(ContentBlockKind.Text, "An integer is even when it is divisible by two.")])],
            [q], [], [new("short", "Quick check", 1, 5, AssessmentSize.Short, ["parity"], [QuestionKind.Single])], [], Readiness: new());
```

and the `search.json` artifact:

```csharp
        ["search.json"] = Json.Write(p.Lessons.Select(l => new { l.Id, l.Title, Text = string.Join("\n", l.Blocks.Select(b => RichText.PlainText(b))), l.ObjectiveIds }))
```

- [ ] **Step 9: Move existing tests onto source-level packs**

Tests can no longer publish a compiled `Pack` as source, because the release model is not a source format. They derive schema 1 sources from `TestPacks.V1()` instead.

`CoreTests.cs`:
- In `Composer_preserves_scenarios_and_rejects_impossible_blueprints` and `Scenario_groups_cannot_be_split_by_objective_filtering`, replace `new("case", "Case", "Shared background")` with `new("case", "Case", RichText.Literal("Shared background"))`.
- In `Every_bundled_pack_compiles_and_malformed_contracts_fail_closed`, replace `Json.Write(Demo())` with `Json.Write(TestPacks.V1())`.

`GoalPolicyTests.cs`: change `private static string Without(Pack pack, params string[] properties)` to take `V1Pack pack`, and replace every `Without(CoreTests.Demo(), …)` with `Without(TestPacks.V1(), …)`.

`LearningRecordTests.cs`:

```csharp
    private async Task<HttpClient> Publish(V1Pack source, HttpClient? publisher = null)
    {
        publisher ??= await TestApi.Publisher(factory);
        (await publisher.PostAsync("/api/authoring/publish", TestPacks.Content(source))).EnsureSuccessStatusCode();
        return publisher;
    }
```

- `Lesson_progress_survives_new_releases_and_flags_revised_lessons`: `var v1 = TestPacks.V1() with { Id = NewId("carry") };` (the `v2` line compiles unchanged against `V1Lesson`).
- `Removed_lessons_leave_progress_without_errors`:

```csharp
        var source = TestPacks.V1();
        var extra = source.Lessons[0] with { Id = "sets-extra", Title = "More sets" };
        var v1 = source with { Id = NewId("removed"), Lessons = [.. source.Lessons, extra] };
        var publisher = await Publish(v1);
        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/sets-extra", null);
        await publisher.PutAsync($"/api/me/courses/{v1.Id}/lessons/sets-intro", null);
        await Publish(v1 with { Version = "1.1.0", Lessons = source.Lessons }, publisher);
```

- `Mastery_goal_packs_report_objectives_instead_of_readiness`: `var mastery = TestPacks.V1() with { Id = NewId("mastery"), Goal = CourseGoal.Mastery, Readiness = null };`
- `Completion_goal_is_met_when_every_lesson_is_read`: `var completion = TestPacks.V1() with { Id = NewId("complete"), Goal = CourseGoal.Completion, Readiness = null };`

`ApiTests.cs`, in `Publishing_is_immutable_and_does_not_rewrite_existing_attempts`:

```csharp
        var pack = TestPacks.V1() with { Id = "publication-" + Guid.NewGuid().ToString("N") };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/authoring/publish", TestPacks.Content(pack))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/authoring/publish", TestPacks.Content(pack))).StatusCode);
```

and later in the same test:

```csharp
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/authoring/publish", TestPacks.Content(pack with { Version = "2.0.0", Title = "New title" }))).StatusCode);
```

- [ ] **Step 10: Run the full suite**

Run: `dotnet build LearnForge.slnx -c Release && dotnet test LearnForge.slnx && make check-content`
Expected: build succeeds with no warnings; every test passes, including the three new `LegacyContentTests`. If an unrelated test fails because it compared `Grade.Explanation` or built a `Pack` directly, update it to the new shapes above without weakening its assertion.

- [ ] **Step 11: Commit**

```bash
git add -A src/LearnForge.Core apps/api tools/cli tests/LearnForge.Tests
git commit -m "feat: releases carry the rich-text AST with schema 1 upcasting

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Attempts saved before rich content keep working

**Files:**
- Create: `apps/api/Services/Attempts/V1AttemptSnapshot.cs`, `apps/api/Services/Attempts/StoredFeedback.cs`
- Modify: `apps/api/Services/Attempts/AttemptService.cs`, `apps/api/Services/Learning/EvidenceBackfill.cs`
- Test: `tests/LearnForge.Tests/LegacyAttemptTests.cs`

**Interfaces:**
- Consumes: `V1Upcaster.ToQuestion/ToScenario`, `AttemptSnapshot.ContentSchema` (Task 6).
- Produces: `StoredFeedback.Read(string json) : Dictionary<string, Grade>`; `V1AttemptSnapshot(string Title, string Version, Objective[] Objectives, V1Scenario[] Scenarios, Blueprint Blueprint, V1Question[] Questions, ReadinessPolicy Readiness)`; `AttemptService.Snapshot` upcasts legacy snapshots.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using LearnForge.Api.Services.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class LegacyAttemptTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task Attempts_saved_before_rich_content_still_render_and_submit()
    {
        var client = await TestApi.Account(factory);
        var userId = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;
        var v1 = TestPacks.V1();
        var question = v1.Questions.First(q => q.Kind == QuestionKind.Single);
        // The exact shapes stored before schema 2: no contentSchema, and explanation text inside each grade.
        var snapshot = new V1AttemptSnapshot(v1.Title, v1.Version, v1.Objectives, v1.Scenarios, v1.Blueprints[0], [question], v1.Readiness ?? new());
        var feedback = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [question.Id] = new { earned = 1, possible = 1, fullyCorrect = true, correct = question.Grading.Correct, matches = (object?)null, explanation = question.Explanation }
        });
        string attemptId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var releaseId = await db.Packs.Where(p => p.PackId == "reasoning-foundations").Select(p => p.Id).FirstAsync();
            var attempt = new Attempt
            {
                UserId = userId, PackReleaseId = releaseId, PackId = "reasoning-foundations", StartKey = Guid.NewGuid().ToString(),
                ActiveKey = userId + ":reasoning-foundations", Mode = AssessmentMode.Learn, SnapshotJson = Json.Write(snapshot),
                AnswersJson = Json.Write(new Dictionary<string, Answer> { [question.Id] = new(question.Grading.Correct, []) }),
                FeedbackJson = feedback, Deadline = DateTime.UtcNow.AddHours(1)
            };
            db.Attempts.Add(attempt);
            await db.SaveChangesAsync();
            attemptId = attempt.Id;
        }

        var view = await client.GetFromJsonAsync<JsonElement>($"/api/me/attempts/{attemptId}");
        var prompt = view.GetProperty("questions")[0].GetProperty("prompt")[0];
        Assert.Equal("paragraph", prompt.GetProperty("kind").GetString());
        Assert.Equal(question.Prompt, prompt.GetProperty("inlines")[0].GetProperty("text").GetString());
        var released = view.GetProperty("feedback").GetProperty(question.Id);
        Assert.True(released.GetProperty("grade").GetProperty("fullyCorrect").GetBoolean());
        Assert.Equal(question.Explanation, released.GetProperty("explanation")[0].GetProperty("inlines")[0].GetProperty("text").GetString());

        (await client.PostAsJsonAsync($"/api/me/attempts/{attemptId}/submit", new { revision = 0 })).EnsureSuccessStatusCode();
        using var check = factory.Services.CreateScope();
        Assert.Equal(1, await check.ServiceProvider.GetRequiredService<AppDb>().Evidence.CountAsync(e => e.AttemptId == attemptId));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.LegacyAttemptTests"`
Expected: build FAIL, `V1AttemptSnapshot` not found.

- [ ] **Step 3: Implement**

`apps/api/Services/Attempts/V1AttemptSnapshot.cs`:

```csharp
namespace LearnForge.Api.Services.Attempts;

// The snapshot shape stored before rich content (no contentSchema property). Read-only compatibility.
public sealed record V1AttemptSnapshot(string Title, string Version, Objective[] Objectives, V1Scenario[] Scenarios,
    Blueprint Blueprint, V1Question[] Questions, ReadinessPolicy Readiness);
```

`apps/api/Services/Attempts/StoredFeedback.cs`:

```csharp
using System.Text.Json.Nodes;

namespace LearnForge.Api.Services.Attempts;

// Feedback rows written before rich content also stored the explanation text. Explanations now come from the
// snapshot, so the old property is dropped on read; the row itself is never rewritten.
public static class StoredFeedback
{
    public static Dictionary<string, Grade> Read(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        foreach (var (_, grade) in node) (grade as JsonObject)?.Remove("explanation");
        return node.Deserialize<Dictionary<string, Grade>>(Json.Options) ?? new();
    }
}
```

In `AttemptService.cs`, add `using System.Text.Json;` and replace `Snapshot`:

```csharp
    // Snapshots saved before rich content have no contentSchema property; their schema 1 questions are upcast on read.
    public static AttemptSnapshot Snapshot(Attempt a)
    {
        using var document = JsonDocument.Parse(a.SnapshotJson, new JsonDocumentOptions { MaxDepth = Json.Options.MaxDepth });
        var root = document.RootElement;
        if (root.TryGetProperty("contentSchema", out _)) return root.Deserialize<AttemptSnapshot>(Json.Options)!;
        var v1 = root.Deserialize<V1AttemptSnapshot>(Json.Options)!;
        return new(v1.Title, v1.Version, v1.Objectives, v1.Scenarios.Select(V1Upcaster.ToScenario).ToArray(), v1.Blueprint,
            v1.Questions.Select(V1Upcaster.ToQuestion).ToArray(), v1.Readiness, Pack.CurrentFormat);
    }
```

Replace every `Json.Read<Dictionary<string, Grade>>(a.FeedbackJson)` in `AttemptService.cs` (three places: `Save`, `Finish`, `View`) and `Json.Read<Dictionary<string, Grade>>(attempt.FeedbackJson)` in `EvidenceBackfill.cs` with `StoredFeedback.Read(…FeedbackJson)`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: PASS, including `LegacyAttemptTests` and the existing `BackfillTests`.

- [ ] **Step 5: Commit**

```bash
git add apps/api tests/LearnForge.Tests/LegacyAttemptTests.cs
git commit -m "feat(api): read attempts saved before rich content without rewriting them

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Compile schema 2 sources

**Files:**
- Create: `src/LearnForge.Core/Domain/Source/SourcePack.cs`, `SourceCatalog.cs`, `SourceLesson.cs`, `SourceModule.cs`, `SourceGlossaryEntry.cs`, `SourceQuestion.cs`, `SourceOption.cs`, `SourceSlot.cs`, `SourceScenario.cs`, `SourceBlock.cs`, `SourceTextBlock.cs`, `SourceCalloutBlock.cs`, `SourceExampleBlock.cs`, `SourceCodeBlock.cs`
- Create: `src/LearnForge.Core/Services/Content/PackCompiler.cs`, `RichWalk.cs`, `RichContentRules.cs`
- Modify: `src/LearnForge.Core/Services/Content/ContentEngine.cs`
- Create: `tests/LearnForge.Tests/Fixtures/minimal-v2.json`
- Modify: `tests/LearnForge.Tests/TestPacks.cs`
- Test: `tests/LearnForge.Tests/SourcePackTests.cs`

**Interfaces:**
- Consumes: `RichTextCompiler`, `TexParser`, `LanguageTag`, release records, `ContentEngine.CheckIds/CheckQuestion/IsIdentifier`.
- Produces: the source records below; `internal sealed class PackCompiler(List<Diagnostic>)` with `Compile(SourcePack) : Pack`; `internal static class RichWalk` with `Inlines(Pack) : IEnumerable<(string Path, RichInline Node)>`, `FromBlocks`, `FromInlines`, `FromQuestion(DeliveryQuestion)` and `FromLessonBlock(LessonBlock)`; `internal static class RichContentRules` with `Check(Pack, List<Diagnostic>)`. `ContentEngine.Compile` accepts schema 1 and 2. Test helpers `TestPacks.MinimalV2() : JsonObject`, `TestPacks.Lesson(JsonObject) : JsonObject`, `TestPacks.Blocks(JsonObject) : JsonArray`.

- [ ] **Step 1: Add the fixture and helpers**

`tests/LearnForge.Tests/Fixtures/minimal-v2.json`:

```json
{
  "schemaVersion": 2,
  "id": "minimal",
  "version": "1.0.0",
  "title": "Minimal pack",
  "description": "A small schema 2 pack for compiler tests.",
  "license": "CC0-1.0",
  "language": "en",
  "catalog": { "subject": "Mathematics", "level": "introductory", "tags": ["sets"], "estimatedHours": 1 },
  "objectives": [{ "id": "sets", "title": "Reason about sets", "prerequisites": [] }],
  "glossary": [{ "id": "union", "term": "union", "definition": "The set of elements in *either* set." }],
  "lessons": [{
    "id": "intro", "title": "Sets", "summary": "Meet sets.", "objectiveIds": ["sets"], "minutes": 10,
    "blocks": [
      { "id": "what", "kind": "text", "body": "A **set** has distinct elements, such as $\\{1, 2\\}$. See the [union](term:union) and [the example](#example)." },
      { "id": "example", "kind": "example", "title": "Example", "body": "- one\n- two" },
      { "id": "code", "kind": "code", "language": "python", "code": "print({1, 2})" }
    ]
  }],
  "questions": [{
    "id": "q1", "familyId": "q1", "kind": "single", "prompt": "Which set is $\\{1\\} \\cup \\{2\\}$?", "objectiveIds": ["sets"],
    "options": [{ "id": "a", "text": "$\\{1, 2\\}$" }, { "id": "b", "text": "$\\{1\\}$" }],
    "slots": [], "selectCount": 1, "reuse": false,
    "grading": { "policy": "exact", "correct": ["a"], "matches": null },
    "explanation": "The union keeps elements from *either* set."
  }],
  "blueprints": [{ "id": "short", "title": "Short", "count": 1, "minutes": 5, "size": "short", "objectiveIds": ["sets"], "requiredKinds": ["single"], "lockSections": false }]
}
```

Add to `TestPacks.cs` (with `using System.Text.Json.Nodes;`):

```csharp
    public static JsonObject MinimalV2() => JsonNode.Parse(File.ReadAllText(Fixture("minimal-v2.json")))!.AsObject();

    public static JsonObject Lesson(JsonObject pack) => pack["lessons"]![0]!.AsObject();

    public static JsonArray Blocks(JsonObject pack) => Lesson(pack)["blocks"]!.AsArray();
```

- [ ] **Step 2: Write the failing test**

`tests/LearnForge.Tests/SourcePackTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Xunit;

namespace LearnForge.Tests;

public class SourcePackTests
{
    private static Compilation Compile(JsonObject pack) => ContentEngine.Compile(pack.ToJsonString());

    private static Pack Success(JsonObject pack)
    {
        var compiled = Compile(pack);
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        return compiled.Pack!;
    }

    private static Diagnostic Failure(JsonObject pack, string code) => Assert.Single(Compile(pack).Diagnostics, d => d.Code == code);

    [Fact] public void A_minimal_schema_2_pack_compiles_into_the_ast()
    {
        var pack = Success(TestPacks.MinimalV2());
        Assert.Equal(Pack.CurrentFormat, pack.Format);
        Assert.Equal(2, pack.SchemaVersion);
        Assert.Equal("en", pack.Language);
        Assert.Equal(TextDirection.Ltr, pack.Direction);
        Assert.Equal("Mathematics", pack.Catalog!.Subject);
        Assert.Equal(CourseLevel.Introductory, pack.Catalog.Level);
        Assert.Equal(new[] { "sets" }, pack.Catalog.Tags);
        var lesson = Assert.Single(pack.Lessons);
        var inlines = Assert.IsType<ParagraphNode>(Assert.Single(Assert.IsType<TextBlock>(lesson.Blocks[0]).Body)).Inlines;
        Assert.Contains(inlines, n => n is TermNode { TermId: "union" });
        Assert.Contains(inlines, n => n is LessonLinkNode { LessonId: "intro", BlockId: "example" });
        Assert.Contains(inlines, n => n is MathInlineNode);
        Assert.IsType<ListNode>(Assert.Single(Assert.IsType<ExampleBlock>(lesson.Blocks[1]).Body));
        Assert.Equal("python", Assert.IsType<CodeSampleBlock>(lesson.Blocks[2]).Language);
        Assert.IsType<MathInlineNode>(Assert.Single(Assert.Single(pack.Questions).Options[0].Text));
        Assert.Equal("union", Assert.Single(pack.Glossary).Id);
        Assert.Equal(ContentHash.Of(lesson), pack.LessonHashes["intro"]);
    }

    [Fact] public void Malformed_sources_fail_closed()
    {
        var unknownKind = TestPacks.MinimalV2();
        TestPacks.Blocks(unknownKind).Add(JsonNode.Parse("""{ "id": "v", "kind": "video", "body": "x" }"""));
        Assert.Equal("LF001", Assert.Single(Compile(unknownKind).Diagnostics).Code);
        var noKind = TestPacks.MinimalV2();
        TestPacks.Blocks(noKind).Add(JsonNode.Parse("""{ "id": "v", "body": "x" }"""));
        Assert.Equal("LF001", Assert.Single(Compile(noKind).Diagnostics).Code);
        var extra = TestPacks.MinimalV2();
        extra["unexpected"] = true;
        Assert.Equal("LF001", Assert.Single(Compile(extra).Diagnostics).Code);
        var noCatalog = TestPacks.MinimalV2();
        noCatalog.Remove("catalog");
        Assert.Equal("LF001", Assert.Single(Compile(noCatalog).Diagnostics).Code);
        var future = TestPacks.MinimalV2();
        future["schemaVersion"] = 3;
        Assert.Equal("LF100", Assert.Single(Compile(future).Diagnostics).Code);
    }

    [Fact] public void Markdown_diagnostics_carry_field_paths_and_guidance()
    {
        var pack = TestPacks.MinimalV2();
        TestPacks.Blocks(pack)[0]!["body"] = "Fine.\n\n# Heading";
        var diagnostic = Failure(pack, "A11Y_STRUCTURE");
        Assert.Equal("lessons.intro.blocks.what.body:3:1", diagnostic.Path);
        Assert.NotNull(diagnostic.Guidance);
    }

    [Theory]
    [InlineData("xx", "ltr", "language")]
    [InlineData("ar", "ltr", "direction")]
    [InlineData("en", "rtl", "direction")]
    public void Language_and_direction_must_agree(string language, string direction, string path)
    {
        var pack = TestPacks.MinimalV2();
        pack["language"] = language;
        pack["direction"] = direction;
        Assert.Equal(path, Failure(pack, "A11Y_LANGUAGE").Path);
    }

    [Fact] public void Right_to_left_packs_compile()
    {
        var pack = TestPacks.MinimalV2();
        pack["language"] = "he";
        pack["direction"] = "rtl";
        Assert.Equal(TextDirection.Rtl, Success(pack).Direction);
    }

    [Fact] public void Lesson_and_block_languages_are_validated()
    {
        var lesson = TestPacks.MinimalV2();
        TestPacks.Lesson(lesson)["language"] = "zz";
        Assert.Equal("lessons.intro.language", Failure(lesson, "A11Y_LANGUAGE").Path);
        var block = TestPacks.MinimalV2();
        TestPacks.Blocks(block)[0]!["language"] = "ar";
        Assert.Equal(TextDirection.Rtl, Assert.IsType<TextBlock>(Success(block).Lessons[0].Blocks[0]).Direction);
    }

    [Fact] public void Catalog_metadata_is_bounded()
    {
        foreach (var mutate in new Action<JsonObject>[]
        {
            c => c["subject"] = "",
            c => c["subject"] = new string('s', 61),
            c => c["tags"] = new JsonArray(Enumerable.Range(0, 11).Select(i => (JsonNode?)JsonValue.Create($"t{i}")).ToArray()),
            c => c["tags"] = new JsonArray("Not An Id"),
            c => c["estimatedHours"] = 0.1
        })
        {
            var pack = TestPacks.MinimalV2();
            mutate(pack["catalog"]!.AsObject());
            Assert.Equal("catalog", Failure(pack, "LF100").Path);
        }
    }

    [Fact] public void Modules_set_reading_order_and_cover_each_lesson_once()
    {
        var pack = TestPacks.MinimalV2();
        var second = TestPacks.Lesson(pack).DeepClone().AsObject();
        second["id"] = "second";
        second["blocks"] = JsonNode.Parse("""[{ "id": "only", "kind": "text", "body": "More." }]""");
        pack["lessons"]!.AsArray().Add(second);
        pack["modules"] = JsonNode.Parse("""[{ "id": "later", "title": "Later", "lessonIds": ["second"] }, { "id": "first", "title": "First", "lessonIds": ["intro"] }]""");
        Assert.Equal(new[] { "second", "intro" }, Success(pack).Lessons.Select(l => l.Id));
        pack["modules"] = JsonNode.Parse("""[{ "id": "first", "title": "First", "lessonIds": ["intro"] }]""");
        Assert.Equal("modules", Failure(pack, "LF100").Path);
        pack["modules"] = JsonNode.Parse("""[{ "id": "first", "title": "First", "lessonIds": ["intro", "second", "intro"] }]""");
        Assert.Equal("modules", Failure(pack, "LF100").Path);
    }

    [Theory]
    [InlineData("[x](term:missing)")]
    [InlineData("[x](lesson:missing)")]
    [InlineData("[x](lesson:intro#missing)")]
    [InlineData("[x](#missing)")]
    public void References_must_resolve(string body)
    {
        var pack = TestPacks.MinimalV2();
        TestPacks.Blocks(pack)[0]!["body"] = body;
        Assert.Equal("lessons.intro.blocks.what", Failure(pack, "LF203").Path);
    }

    [Fact] public void Dropdown_options_must_be_plain_text()
    {
        var pack = TestPacks.MinimalV2();
        pack["questions"]!.AsArray().Add(JsonNode.Parse("""
            { "id": "q2", "familyId": "q2", "kind": "dropdown", "prompt": "Complete it.", "objectiveIds": ["sets"], "options": [],
              "slots": [{ "id": "s", "text": "$1 \\in \\{1\\}$", "options": [{ "id": "t", "text": "*True*" }, { "id": "f", "text": "False" }] }],
              "selectCount": 1, "reuse": false, "grading": { "policy": "exact", "correct": [], "matches": { "s": "t" } }, "explanation": "Yes." }
            """));
        Assert.StartsWith("questions.q2.slots.s.options.t", Failure(pack, "LF201").Path);
    }

    [Fact] public void Code_blocks_need_a_language_label()
    {
        var pack = TestPacks.MinimalV2();
        TestPacks.Blocks(pack)[2]!["language"] = "Python!";
        Assert.Equal("lessons.intro.blocks.code", Failure(pack, "LF100").Path);
    }

    [Fact] public void Block_ids_are_unique_within_a_lesson()
    {
        var pack = TestPacks.MinimalV2();
        TestPacks.Blocks(pack)[1]!["id"] = "what";
        Assert.Equal("lessons.intro.blocks.what", Failure(pack, "A11Y_STRUCTURE").Path);
    }

    [Fact] public void Schema_2_questions_keep_the_question_contract()
    {
        var pack = TestPacks.MinimalV2();
        pack["questions"]![0]!["grading"]!["correct"] = new JsonArray("z");
        Assert.Contains(Compile(pack).Diagnostics, d => d.Code == "LF100" && d.Message == "Invalid option/key contract.");
    }

    [Fact] public void Glossary_entries_need_unique_ids_and_definitions()
    {
        var duplicate = TestPacks.MinimalV2();
        duplicate["glossary"]!.AsArray().Add(JsonNode.Parse("""{ "id": "union", "term": "union", "definition": "Again." }"""));
        Assert.Equal("glossary", Failure(duplicate, "LF100").Path);
        var blank = TestPacks.MinimalV2();
        blank["glossary"]![0]!["definition"] = " ";
        Assert.Equal("glossary.union", Failure(blank, "LF100").Path);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.SourcePackTests"`
Expected: FAIL. The minimal pack is rejected with `LF100 schemaVersion: Only schema version 1 is supported.`

- [ ] **Step 4: Create the source records**

Every file uses `namespace LearnForge.Core;`.

`Domain/Source/SourcePack.cs`:

```csharp
// Schema 2 source, exactly as authors write it. Rich fields are Markdown strings; PackCompiler turns them into the AST.
public sealed record SourcePack(int SchemaVersion, string Id, string Version, string Title, string Description, string License,
    string Language, SourceCatalog Catalog, Objective[] Objectives, SourceLesson[] Lessons, SourceQuestion[] Questions,
    Blueprint[] Blueprints, TextDirection Direction = TextDirection.Ltr, SourceModule[]? Modules = null,
    SourceGlossaryEntry[]? Glossary = null, SourceScenario[]? Scenarios = null, SourceReference[]? Sources = null,
    ReadinessPolicy? Readiness = null, CourseGoal Goal = CourseGoal.Readiness, MasteryPolicy? Mastery = null);
```

```csharp
public sealed record SourceCatalog(string Subject, CourseLevel Level, string[]? Tags = null, decimal? EstimatedHours = null);
```
```csharp
public sealed record SourceLesson(string Id, string Title, string Summary, string[] ObjectiveIds, SourceBlock[] Blocks,
    int? Minutes = null, string? Language = null);
```
```csharp
public sealed record SourceModule(string Id, string Title, string[] LessonIds);
```
```csharp
public sealed record SourceGlossaryEntry(string Id, string Term, string Definition);
```
```csharp
public sealed record SourceQuestion(string Id, string FamilyId, QuestionKind Kind, string Prompt, string[] ObjectiveIds,
    SourceOption[] Options, SourceSlot[] Slots, int SelectCount, bool Reuse, Grading Grading, string Explanation,
    string? ScenarioId = null, decimal Weight = 1);
```
```csharp
public sealed record SourceOption(string Id, string Text);
```
```csharp
public sealed record SourceSlot(string Id, string Text, SourceOption[] Options);
```
```csharp
public sealed record SourceScenario(string Id, string Title, string Background);
```

`Domain/Source/SourceBlock.cs`:

```csharp
using System.Text.Json.Serialization;

namespace LearnForge.Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SourceTextBlock), "text")]
[JsonDerivedType(typeof(SourceCalloutBlock), "callout")]
[JsonDerivedType(typeof(SourceExampleBlock), "example")]
[JsonDerivedType(typeof(SourceCodeBlock), "code")]
public abstract record SourceBlock
{
    public abstract string Id { get; init; }
}
```

```csharp
public sealed record SourceTextBlock(string Id, string Body, string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
public sealed record SourceCalloutBlock(string Id, string Body, string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
public sealed record SourceExampleBlock(string Id, string Body, string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
// Language is the programming language label, e.g. python; code blocks take no human-language override.
public sealed record SourceCodeBlock(string Id, string Language, string Code, string? Title = null) : SourceBlock;
```

- [ ] **Step 5: Implement the pack compiler**

`Services/Content/PackCompiler.cs`:

```csharp
namespace LearnForge.Core;

// Compiles a schema 2 source into the release model. Every rich field is parsed exactly once, here.
internal sealed class PackCompiler
{
    private readonly RichTextCompiler rich;

    public PackCompiler(List<Diagnostic> diagnostics) => rich = new RichTextCompiler(diagnostics);

    public Pack Compile(SourcePack s)
    {
        var glossary = (s.Glossary ?? []).Select(g => new GlossaryEntry(g.Id, g.Term, rich.Inlines(g.Definition, $"glossary.{g.Id}.definition"))).ToArray();
        var modules = (s.Modules ?? []).Select(m => new Module(m.Id, m.Title, m.LessonIds)).ToArray();
        var lessons = InModuleOrder(s.Lessons.Select(CompileLesson).ToArray(), modules);
        var scenarios = (s.Scenarios ?? []).Select(sc => new Scenario(sc.Id, sc.Title, rich.Blocks(sc.Background, $"scenarios.{sc.Id}.background"))).ToArray();
        var catalog = new CatalogMetadata(s.Catalog.Subject, s.Catalog.Level, s.Catalog.Tags ?? [], s.Catalog.EstimatedHours);
        return new Pack(Pack.CurrentFormat, s.SchemaVersion, s.Id, s.Version, s.Title, s.Description, s.License, s.Language,
            s.Direction, catalog, s.Objectives, modules, glossary, lessons, s.Questions.Select(CompileQuestion).ToArray(), scenarios,
            s.Blueprints, s.Sources ?? [], [], lessons.DistinctBy(l => l.Id).ToDictionary(l => l.Id, l => ContentHash.Of(l)),
            s.Readiness, s.Goal, s.Mastery);
    }

    // Modules define reading order. Lessons outside every module (a validation error) keep source order at the end.
    private static Lesson[] InModuleOrder(Lesson[] lessons, Module[] modules)
    {
        if (modules.Length == 0) return lessons;
        var position = modules.SelectMany(m => m.LessonIds).Select((id, index) => (id, index)).DistinctBy(p => p.id).ToDictionary(p => p.id, p => p.index);
        return lessons.OrderBy(l => position.GetValueOrDefault(l.Id, int.MaxValue)).ToArray();
    }

    private static TextDirection? DirectionOf(string? language) => LanguageTag.IsValid(language) ? LanguageTag.DirectionOf(language) : null;

    private Lesson CompileLesson(SourceLesson l) =>
        new(l.Id, l.Title, l.Summary, l.ObjectiveIds, l.Blocks.Select(b => CompileBlock(l, b)).ToArray(), l.Minutes, l.Language, DirectionOf(l.Language));

    private LessonBlock CompileBlock(SourceLesson lesson, SourceBlock block)
    {
        var at = $"lessons.{lesson.Id}.blocks.{block.Id}";
        RichBlock[] Blocks(string text, string field) => rich.Blocks(text, $"{at}.{field}", lesson.Id);
        return block switch
        {
            SourceTextBlock b => new TextBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Body, "body")),
            SourceCalloutBlock b => new CalloutBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Body, "body")),
            SourceExampleBlock b => new ExampleBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Body, "body")),
            SourceCodeBlock b => new CodeSampleBlock(b.Id, b.Title, b.Language, b.Code),
            _ => throw new InvalidOperationException($"Unsupported block kind in {at}.")
        };
    }

    private Question CompileQuestion(SourceQuestion q)
    {
        var at = $"questions.{q.Id}";
        return new(q.Id, q.FamilyId, q.Kind, rich.Blocks(q.Prompt, $"{at}.prompt"), q.ObjectiveIds,
            q.Options.Select(o => CompileOption(o, $"{at}.options.{o.Id}", null, plain: false)).ToArray(),
            q.Slots.Select(s => CompileSlot(q.Kind, s, $"{at}.slots.{s.Id}", null)).ToArray(),
            q.SelectCount, q.Reuse, q.Grading, rich.Blocks(q.Explanation, $"{at}.explanation"), q.ScenarioId, q.Weight);
    }

    private Option CompileOption(SourceOption o, string at, string? lessonId, bool plain) => new(o.Id, rich.Inlines(o.Text, at, lessonId, plain));

    // Dropdown options render inside native <option> elements, which hold text only.
    private Slot CompileSlot(QuestionKind kind, SourceSlot s, string at, string? lessonId) =>
        new(s.Id, rich.Inlines(s.Text, $"{at}.text", lessonId),
            s.Options.Select(o => CompileOption(o, $"{at}.options.{o.Id}", lessonId, kind == QuestionKind.Dropdown)).ToArray());
}
```

- [ ] **Step 6: Implement the walker and the rules**

`Services/Content/RichWalk.cs`:

```csharp
namespace LearnForge.Core;

// Enumerates the inline nodes of every rich field, so references are validated in one place.
internal static class RichWalk
{
    public static IEnumerable<(string Path, RichInline Node)> Inlines(Pack p)
    {
        foreach (var g in p.Glossary) foreach (var n in FromInlines(g.Definition)) yield return ($"glossary.{g.Id}", n);
        foreach (var l in p.Lessons) foreach (var b in l.Blocks) foreach (var n in FromLessonBlock(b)) yield return ($"lessons.{l.Id}.blocks.{b.Id}", n);
        foreach (var q in p.Questions) foreach (var n in FromQuestion(DeliveryQuestion.From(q)).Concat(FromBlocks(q.Explanation))) yield return ($"questions.{q.Id}", n);
        foreach (var s in p.Scenarios) foreach (var n in FromBlocks(s.Background)) yield return ($"scenarios.{s.Id}", n);
        foreach (var k in p.Checks) foreach (var n in FromBlocks(k.Explanation)) yield return ($"lessons.{k.LessonId}.blocks.{k.BlockId}", n);
    }

    public static IEnumerable<RichInline> FromBlocks(IEnumerable<RichBlock> blocks) => blocks.SelectMany(block => block switch
    {
        ParagraphNode p => FromInlines(p.Inlines),
        ListNode l => l.Items.SelectMany(item => FromBlocks(item)),
        QuoteNode q => FromBlocks(q.Blocks),
        _ => Array.Empty<RichInline>()
    });

    public static IEnumerable<RichInline> FromInlines(IEnumerable<RichInline> inlines) => inlines.SelectMany(node => node switch
    {
        EmphasisNode e => FromInlines(e.Inlines).Prepend(node),
        StrongNode s => FromInlines(s.Inlines).Prepend(node),
        LinkNode l => FromInlines(l.Inlines).Prepend(node),
        LessonLinkNode l => FromInlines(l.Inlines).Prepend(node),
        TermNode t => FromInlines(t.Inlines).Prepend(node),
        LangNode g => FromInlines(g.Inlines).Prepend(node),
        _ => new[] { node }
    });

    public static IEnumerable<RichInline> FromQuestion(DeliveryQuestion q) =>
        FromBlocks(q.Prompt).Concat(q.Options.SelectMany(o => FromInlines(o.Text)))
            .Concat(q.Slots.SelectMany(s => FromInlines(s.Text).Concat(s.Options.SelectMany(o => FromInlines(o.Text)))));

    // Task 9 extends this switch with the new block kinds.
    public static IEnumerable<RichInline> FromLessonBlock(LessonBlock block) => block switch
    {
        TextBlock b => FromBlocks(b.Body),
        CalloutBlock b => FromBlocks(b.Body),
        ExampleBlock b => FromBlocks(b.Body),
        _ => Array.Empty<RichInline>()
    };
}
```

`Services/Content/RichContentRules.cs`:

```csharp
namespace LearnForge.Core;

// Schema 2 rules: language, catalog, modules, glossary, block structure and references. They read the compiled
// release, so tests and tools can validate a modified Pack directly. Schema 1 content is literal and has none of these features.
internal static class RichContentRules
{
    public static void Check(Pack p, List<Diagnostic> errors)
    {
        if (p.SchemaVersion < 2) return;
        Language(p.Language, "language", errors, required: true);
        if (LanguageTag.IsValid(p.Language) && LanguageTag.DirectionOf(p.Language) != p.Direction)
            Add(errors, "A11Y_LANGUAGE", "direction",
                $"Text in '{p.Language}' runs {Name(LanguageTag.DirectionOf(p.Language))}, but the pack declares {Name(p.Direction)}.",
                "Set direction to match the language's script.");
        Catalog(p.Catalog, errors);
        Modules(p, errors);
        Glossary(p, errors);
        var lessons = p.Lessons.DistinctBy(l => l.Id).ToDictionary(l => l.Id);
        var terms = p.Glossary.Select(g => g.Id).ToHashSet();
        foreach (var lesson in p.Lessons)
        {
            if (lesson.Minutes is < 1 or > 600) Add(errors, "LF100", $"lessons.{lesson.Id}", "Lesson minutes must be between 1 and 600.");
            Language(lesson.Language, $"lessons.{lesson.Id}.language", errors);
            var seen = new HashSet<string>();
            foreach (var block in lesson.Blocks)
            {
                var at = $"lessons.{lesson.Id}.blocks.{block.Id}";
                if (!ContentEngine.IsIdentifier(block.Id)) Add(errors, "LF100", at, "Block IDs use lowercase letters, numbers, dots, underscores or hyphens.");
                if (!seen.Add(block.Id)) Add(errors, "A11Y_STRUCTURE", at, "Block IDs must be unique within a lesson.", "Rename one of the blocks. IDs anchor links and future notes.");
                Block(p, lesson, block, at, terms, errors);
            }
        }
        foreach (var (at, node) in RichWalk.Inlines(p)) Reference(node, at, lessons, terms, errors);
        foreach (var q in p.Questions.Where(q => q.Kind == QuestionKind.Dropdown))
            if (q.Slots.SelectMany(s => s.Options).Any(o => o.Text.Any(n => n is not TextNode)))
                Add(errors, "LF201", $"questions.{q.Id}", "Dropdown options render in a native select and accept plain text only.", "Remove formatting, math and links from the options.");
    }

    // Per-kind rules. Task 9 adds the new block kinds.
    private static void Block(Pack p, Lesson lesson, LessonBlock block, string at, HashSet<string> terms, List<Diagnostic> errors)
    {
        switch (block)
        {
            case TextBlock b: Body(b.Body, b.Language, at, errors); break;
            case CalloutBlock b: Body(b.Body, b.Language, at, errors); break;
            case ExampleBlock b: Body(b.Body, b.Language, at, errors); break;
            case CodeSampleBlock b:
                if (b.Language is null || !RichTextCompiler.CodeLanguage().IsMatch(b.Language))
                    Add(errors, "LF100", at, "Code blocks need a language label such as python, sql or text.");
                break;
        }
    }

    private static void Body(RichBlock[] body, string? language, string at, List<Diagnostic> errors)
    {
        if (RichText.IsBlank(body)) Add(errors, "LF100", at, "This block has no content.");
        Language(language, at + ".language", errors);
    }

    private static void Catalog(CatalogMetadata? c, List<Diagnostic> errors)
    {
        if (c is null || string.IsNullOrWhiteSpace(c.Subject) || c.Subject.Length > 60 || !Enum.IsDefined(c.Level) || c.Tags.Length > 10
            || c.Tags.Any(t => !ContentEngine.IsIdentifier(t)) || c.Tags.Distinct().Count() != c.Tags.Length || c.EstimatedHours is < 0.5m or > 1000m)
            Add(errors, "LF100", "catalog", "The catalog needs a subject of 1–60 characters, a level, up to 10 distinct tag IDs and estimatedHours between 0.5 and 1000.");
    }

    private static void Modules(Pack p, List<Diagnostic> errors)
    {
        ContentEngine.CheckIds(errors, "modules", p.Modules.Select(m => m.Id));
        if (p.Modules.Length > 100) Add(errors, "LF100", "modules", "Use at most 100 modules.");
        if (p.Modules.Length == 0) return;
        var placed = p.Modules.SelectMany(m => m.LessonIds).ToArray();
        if (p.Modules.Any(m => string.IsNullOrWhiteSpace(m.Title) || m.LessonIds.Length == 0)
            || placed.Distinct().Count() != placed.Length || !placed.ToHashSet().SetEquals(p.Lessons.Select(l => l.Id)))
            Add(errors, "LF100", "modules", "Every module needs a title and lessons, and every lesson must belong to exactly one module.");
    }

    private static void Glossary(Pack p, List<Diagnostic> errors)
    {
        ContentEngine.CheckIds(errors, "glossary", p.Glossary.Select(g => g.Id));
        if (p.Glossary.Length > 2000) Add(errors, "LF100", "glossary", "Use at most 2000 glossary terms.");
        foreach (var g in p.Glossary)
            if (string.IsNullOrWhiteSpace(g.Term) || g.Term.Length > 100 || RichText.IsBlank(g.Definition))
                Add(errors, "LF100", $"glossary.{g.Id}", "Glossary entries need a term of up to 100 characters and a definition.");
    }

    private static void Reference(RichInline node, string at, Dictionary<string, Lesson> lessons, HashSet<string> terms, List<Diagnostic> errors)
    {
        switch (node)
        {
            case LessonLinkNode link when !lessons.TryGetValue(link.LessonId, out var target) || (link.BlockId is { } block && target.Blocks.All(b => b.Id != block)):
                Add(errors, "LF203", at, $"The link target '{link.LessonId}{(link.BlockId is null ? "" : "#" + link.BlockId)}' does not exist.", "Check the lesson and block IDs.");
                break;
            case TermNode term when !terms.Contains(term.TermId):
                Add(errors, "LF203", at, $"The glossary has no term '{term.TermId}'.", "Add the term to the glossary or fix its ID.");
                break;
            case LangNode lang when !LanguageTag.IsValid(lang.Language):
                Add(errors, "A11Y_LANGUAGE", at, $"'{lang.Language}' is not a valid language tag.", "Use a BCP 47 tag such as fr or pt-BR.");
                break;
        }
    }

    private static void Language(string? tag, string path, List<Diagnostic> errors, bool required = false)
    {
        if ((required || tag is not null) && !LanguageTag.IsValid(tag))
            Add(errors, "A11Y_LANGUAGE", path, $"'{tag}' is not a valid language tag.", "Use a BCP 47 tag such as en, pt-BR or zh-Hant.");
    }

    private static string Name(TextDirection direction) => direction == TextDirection.Rtl ? "right to left" : "left to right";

    private static void Add(List<Diagnostic> errors, string code, string path, string message, string? guidance = null) =>
        errors.Add(new(code, path, message, guidance));
}
```

- [ ] **Step 7: Accept schema 2 in the engine**

In `ContentEngine.Compile`, replace the schema check and the release line with:

```csharp
            if (schema is not (1 or 2)) return new(null, [new("LF100", "schemaVersion", "Use schema version 1 or 2.", "Set \"schemaVersion\": 2 for new packs.")], hash);
            var diagnostics = new List<Diagnostic>();
            var release = schema == 2
                ? new PackCompiler(diagnostics).Compile(node!.Deserialize<SourcePack>(Json.Options) ?? throw new JsonException("Missing pack."))
                : V1Upcaster.ToRelease(node!.Deserialize<V1Pack>(Json.Options) ?? throw new JsonException("Missing pack."), diagnostics);
```

In `Validate`, change the schema check to `if (p.SchemaVersion is not (1 or 2)) Error("schemaVersion", "Use schema version 1 or 2.");` and add `RichContentRules.Check(p, errors);` immediately before `return errors.ToArray();`.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.SourcePackTests"`, then `dotnet test LearnForge.slnx`.
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/LearnForge.Core tests/LearnForge.Tests
git commit -m "feat(core): compile schema 2 sources with language, catalog, modules and glossary

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Math, table, worked example, misconception, definition, primary source and inline check blocks

**Files:**
- Create (source): `Domain/Source/SourceMathBlock.cs`, `SourceTableBlock.cs`, `SourceWorkedExampleBlock.cs`, `SourceMisconceptionBlock.cs`, `SourceDefinitionBlock.cs`, `SourcePrimarySourceBlock.cs`, `SourceInlineCheckBlock.cs`, `SourceCheckQuestion.cs`
- Create (release): `Domain/Content/Attribution.cs`, `Domain/Content/Blocks/MathBlock.cs`, `TableBlock.cs`, `WorkedExampleBlock.cs`, `MisconceptionBlock.cs`, `DefinitionBlock.cs`, `PrimarySourceBlock.cs`, `InlineCheckBlock.cs`
- Create: `Services/Content/InlineChecks.cs`
- Modify: `Domain/Source/SourceBlock.cs`, `Domain/Content/Blocks/LessonBlock.cs`, `Services/Content/PackCompiler.cs`, `RichContentRules.cs`, `RichWalk.cs`, `Domain/Rich/RichText.cs`
- Test: `tests/LearnForge.Tests/ContentBlocksTests.cs`

**Interfaces:**
- Produces (release):
  - `Attribution(string Title, string? Author = null, string? Date = null, string? Url = null)`
  - `MathBlock(string Id, string? Title, string? Language, TextDirection? Direction, string Tex, MathNode Root, string Description)`
  - `TableBlock(string Id, string? Title, string? Language, TextDirection? Direction, string Caption, RichInline[][] Columns, RichInline[][][] Rows, bool RowHeaders)`
  - `WorkedExampleBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Problem, RichBlock[][] Steps, RichBlock[] Result)`
  - `MisconceptionBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Claim, RichBlock[] Correction)`
  - `DefinitionBlock(string Id, string? Language, TextDirection? Direction, string TermId, string Term, RichInline[] Definition, RichBlock[]? Body)`
  - `PrimarySourceBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Excerpt, Attribution Attribution, RichBlock[]? Translation)`; here `Language` is the excerpt's language.
  - `InlineCheckBlock(string Id, string? Title, string? Language, TextDirection? Direction, DeliveryQuestion Question)`
  - Discriminators: `math`, `table`, `workedExample`, `misconception`, `definition`, `primarySource`, `inlineCheck`.
  - `InlineChecks.Question(Pack, string lessonId, string blockId) : Question?` (public), `InlineChecks.Build(DeliveryQuestion, InlineCheckKey) : Question` (internal).

- [ ] **Step 1: Write the failing test**

`tests/LearnForge.Tests/ContentBlocksTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Xunit;

namespace LearnForge.Tests;

public class ContentBlocksTests
{
    private const string NewBlocks = """
    [
      { "id": "eq", "kind": "math", "title": "Union size", "tex": "|A \\cup B| = |A| + |B| - |A \\cap B|",
        "description": "The size of the union is the sum of the sizes minus the size of the intersection." },
      { "id": "sizes", "kind": "table", "caption": "Set sizes", "columns": ["Set", "Size"], "rows": [["$A$", "2"], ["$B$", "2"]], "rowHeaders": true },
      { "id": "worked", "kind": "workedExample", "problem": "Find $A \\cup B$.", "steps": ["List $A$.", "Add $B$."], "result": "$\\{1, 2, 3\\}$" },
      { "id": "both", "kind": "misconception", "claim": "Union means *both*.", "correction": "Union means **either**." },
      { "id": "union-def", "kind": "definition", "termId": "union", "body": "Written $A \\cup B$." },
      { "id": "quote", "kind": "primarySource", "excerpt": "Omnia mutantur, nihil interit.", "language": "la",
        "translation": "Everything changes, nothing perishes.", "attribution": { "title": "Metamorphoses", "author": "Ovid" } },
      { "id": "check", "kind": "inlineCheck", "title": "Try it", "question": { "kind": "single", "prompt": "Which is $\\{1\\} \\cup \\{2\\}$?",
        "options": [{ "id": "a", "text": "$\\{1, 2\\}$" }, { "id": "b", "text": "$\\{1\\}$" }], "slots": [], "selectCount": 1, "reuse": false,
        "grading": { "policy": "exact", "correct": ["a"], "matches": null }, "explanation": "The union collects *both* elements." } }
    ]
    """;

    private static JsonObject WithNewBlocks()
    {
        var pack = TestPacks.MinimalV2();
        foreach (var block in JsonNode.Parse(NewBlocks)!.AsArray()) TestPacks.Blocks(pack).Add(block!.DeepClone());
        return pack;
    }

    private static JsonObject Block(JsonObject pack, string id) =>
        TestPacks.Blocks(pack).Single(b => b!["id"]!.GetValue<string>() == id)!.AsObject();

    private static Pack Compiled(JsonObject pack)
    {
        var compiled = ContentEngine.Compile(pack.ToJsonString());
        Assert.True(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}")));
        return compiled.Pack!;
    }

    private static Diagnostic Single(JsonObject pack, string code) =>
        Assert.Single(ContentEngine.Compile(pack.ToJsonString()).Diagnostics, d => d.Code == code);

    [Fact] public void Every_new_block_kind_compiles()
    {
        var blocks = Compiled(WithNewBlocks()).Lessons[0].Blocks.ToDictionary(b => b.Id);
        Assert.NotEmpty(Assert.IsType<Mrow>(Assert.IsType<MathBlock>(blocks["eq"]).Root).Children);
        var table = Assert.IsType<TableBlock>(blocks["sizes"]);
        Assert.Equal(2, table.Rows.Length);
        Assert.True(table.RowHeaders);
        Assert.Equal(2, Assert.IsType<WorkedExampleBlock>(blocks["worked"]).Steps.Length);
        Assert.IsType<MisconceptionBlock>(blocks["both"]);
        var definition = Assert.IsType<DefinitionBlock>(blocks["union-def"]);
        Assert.Equal("union", definition.Term);
        Assert.NotEmpty(definition.Definition);
        var quote = Assert.IsType<PrimarySourceBlock>(blocks["quote"]);
        Assert.Equal("la", quote.Language);
        Assert.Equal("Ovid", quote.Attribution.Author);
        var check = Assert.IsType<InlineCheckBlock>(blocks["check"]);
        Assert.Equal("check", check.Question.Id);
        Assert.Equal(new[] { "sets" }, check.Question.ObjectiveIds);
    }

    [Fact] public void Inline_check_keys_stay_out_of_lessons()
    {
        var pack = Compiled(WithNewBlocks());
        var lessons = Json.Write(pack.Lessons);
        Assert.DoesNotContain("grading", lessons);
        Assert.DoesNotContain("explanation", lessons);
        var key = Assert.Single(pack.Checks);
        Assert.Equal("intro", key.LessonId);
        Assert.Equal("check", key.BlockId);
        var question = InlineChecks.Question(pack, "intro", "check")!;
        Assert.True(Grader.Score(question, new(["a"], [])).FullyCorrect);
        Assert.False(Grader.Score(question, new(["b"], [])).FullyCorrect);
        Assert.Null(InlineChecks.Question(pack, "intro", "eq"));
        Assert.Null(InlineChecks.Question(pack, "missing", "check"));
    }

    [Theory]
    [InlineData("eq", "description", "")]
    [InlineData("sizes", "caption", " ")]
    public void Math_and_tables_need_text_alternatives(string blockId, string field, string value)
    {
        var pack = WithNewBlocks();
        Block(pack, blockId)[field] = value;
        Assert.Equal($"lessons.intro.blocks.{blockId}", Single(pack, "A11Y_ALTERNATIVE").Path);
    }

    [Fact] public void Table_rows_need_one_cell_per_column()
    {
        var pack = WithNewBlocks();
        Block(pack, "sizes")["rows"] = JsonNode.Parse("""[["$A$"]]""");
        Assert.Equal("lessons.intro.blocks.sizes", Single(pack, "A11Y_STRUCTURE").Path);
    }

    [Fact] public void Primary_sources_need_a_language_for_translations_and_https_links()
    {
        var noLanguage = WithNewBlocks();
        Block(noLanguage, "quote").Remove("language");
        Assert.Equal("lessons.intro.blocks.quote", Single(noLanguage, "A11Y_LANGUAGE").Path);
        var http = WithNewBlocks();
        Block(http, "quote")["attribution"]!["url"] = "http://example.org";
        Assert.Equal("lessons.intro.blocks.quote", Single(http, "LF202").Path);
    }

    [Fact] public void Definitions_need_a_known_term()
    {
        var pack = WithNewBlocks();
        Block(pack, "union-def")["termId"] = "missing";
        Assert.Equal("lessons.intro.blocks.union-def", Single(pack, "LF203").Path);
    }

    [Fact] public void Inline_checks_follow_the_question_contract()
    {
        var pack = WithNewBlocks();
        Block(pack, "check")["question"]!["selectCount"] = 2;
        Assert.Contains(ContentEngine.Compile(pack.ToJsonString()).Diagnostics,
            d => d.Path == "lessons.intro.blocks.check" && d.Message == "Single choice needs one key and one selection.");
    }

    [Fact] public void Worked_examples_need_steps()
    {
        var pack = WithNewBlocks();
        Block(pack, "worked")["steps"] = new JsonArray();
        Assert.Equal("lessons.intro.blocks.worked", Single(pack, "LF100").Path);
    }

    [Fact] public void Math_block_errors_point_into_the_tex()
    {
        var pack = WithNewBlocks();
        Block(pack, "eq")["tex"] = "x + \\foo";
        Assert.Equal("lessons.intro.blocks.eq.tex:1:5", Single(pack, "LF301").Path);
    }

    [Fact] public void Every_block_kind_has_search_text() =>
        Assert.All(Compiled(WithNewBlocks()).Lessons[0].Blocks, b => Assert.False(string.IsNullOrWhiteSpace(RichText.PlainText(b)), b.Id));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.ContentBlocksTests"`
Expected: build FAIL (`MathBlock` and the other new records not found).

- [ ] **Step 3: Create the records**

Each file uses `namespace LearnForge.Core;`.

Source records:

```csharp
public sealed record SourceMathBlock(string Id, string Tex, string Description, string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
public sealed record SourceTableBlock(string Id, string Caption, string[] Columns, string[][] Rows, bool RowHeaders = false,
    string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
public sealed record SourceWorkedExampleBlock(string Id, string Problem, string[] Steps, string Result, string? Title = null,
    string? Language = null) : SourceBlock;
```
```csharp
public sealed record SourceMisconceptionBlock(string Id, string Claim, string Correction, string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
// The rendered title is the glossary term, so a definition has no title of its own.
public sealed record SourceDefinitionBlock(string Id, string TermId, string? Body = null, string? Language = null) : SourceBlock;
```
```csharp
// Language is the excerpt's language; a translation is required to state it.
public sealed record SourcePrimarySourceBlock(string Id, string Excerpt, Attribution Attribution, string? Translation = null,
    string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
public sealed record SourceInlineCheckBlock(string Id, SourceCheckQuestion Question, string? Title = null, string? Language = null) : SourceBlock;
```
```csharp
// A bank question without the bank's identity: the block ID, the lesson's objectives and weight 1 apply.
public sealed record SourceCheckQuestion(QuestionKind Kind, string Prompt, SourceOption[] Options, SourceSlot[] Slots,
    int SelectCount, bool Reuse, Grading Grading, string Explanation);
```

Add to `SourceBlock.cs`, after the existing attributes:

```csharp
[JsonDerivedType(typeof(SourceMathBlock), "math")]
[JsonDerivedType(typeof(SourceTableBlock), "table")]
[JsonDerivedType(typeof(SourceWorkedExampleBlock), "workedExample")]
[JsonDerivedType(typeof(SourceMisconceptionBlock), "misconception")]
[JsonDerivedType(typeof(SourceDefinitionBlock), "definition")]
[JsonDerivedType(typeof(SourcePrimarySourceBlock), "primarySource")]
[JsonDerivedType(typeof(SourceInlineCheckBlock), "inlineCheck")]
```

Release records:

```csharp
public sealed record Attribution(string Title, string? Author = null, string? Date = null, string? Url = null);
```
```csharp
// Tex is the author's source; Root is what browsers render; Description is the required plain-language equivalent.
public sealed record MathBlock(string Id, string? Title, string? Language, TextDirection? Direction, string Tex, MathNode Root,
    string Description) : LessonBlock;
```
```csharp
// Simple header relationships only: column headers, plus row headers in the first column when RowHeaders is set.
public sealed record TableBlock(string Id, string? Title, string? Language, TextDirection? Direction, string Caption,
    RichInline[][] Columns, RichInline[][][] Rows, bool RowHeaders) : LessonBlock;
```
```csharp
public sealed record WorkedExampleBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Problem,
    RichBlock[][] Steps, RichBlock[] Result) : LessonBlock;
```
```csharp
public sealed record MisconceptionBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Claim,
    RichBlock[] Correction) : LessonBlock;
```
```csharp
// Term and Definition are copied from the glossary at compile time, so renderers need no lookup.
public sealed record DefinitionBlock(string Id, string? Language, TextDirection? Direction, string TermId, string Term,
    RichInline[] Definition, RichBlock[]? Body) : LessonBlock;
```
```csharp
// Language is the excerpt's language; the translation is in the lesson's language.
public sealed record PrimarySourceBlock(string Id, string? Title, string? Language, TextDirection? Direction, RichBlock[] Excerpt,
    Attribution Attribution, RichBlock[]? Translation) : LessonBlock;
```
```csharp
// The learner-facing half of an inline check; its key is in Pack.Checks.
public sealed record InlineCheckBlock(string Id, string? Title, string? Language, TextDirection? Direction, DeliveryQuestion Question) : LessonBlock;
```

Add to `LessonBlock.cs`, after the existing attributes:

```csharp
[JsonDerivedType(typeof(MathBlock), "math")]
[JsonDerivedType(typeof(TableBlock), "table")]
[JsonDerivedType(typeof(WorkedExampleBlock), "workedExample")]
[JsonDerivedType(typeof(MisconceptionBlock), "misconception")]
[JsonDerivedType(typeof(DefinitionBlock), "definition")]
[JsonDerivedType(typeof(PrimarySourceBlock), "primarySource")]
[JsonDerivedType(typeof(InlineCheckBlock), "inlineCheck")]
```

`Services/Content/InlineChecks.cs`:

```csharp
namespace LearnForge.Core;

public static class InlineChecks
{
    // Rebuilds the gradable question for one inline check from its public block and private key; null if either is missing.
    public static Question? Question(Pack pack, string lessonId, string blockId)
    {
        var lesson = pack.Lessons.FirstOrDefault(l => l.Id == lessonId);
        var block = lesson?.Blocks.OfType<InlineCheckBlock>().FirstOrDefault(b => b.Id == blockId);
        var key = pack.Checks.FirstOrDefault(k => k.LessonId == lessonId && k.BlockId == blockId);
        return block is null || key is null ? null : Build(block.Question, key);
    }

    internal static Question Build(DeliveryQuestion q, InlineCheckKey key) => new(q.Id, "inline-check", q.Kind, q.Prompt, q.ObjectiveIds,
        q.Options, q.Slots, q.SelectCount, q.Reuse, key.Grading, key.Explanation, null, 1);
}
```

- [ ] **Step 4: Compile the new blocks**

In `PackCompiler.cs`:

0. Keep the diagnostics list for `TexParser`. Replace the field and constructor with:

```csharp
    private readonly List<Diagnostic> diagnostics;
    private readonly RichTextCompiler rich;

    public PackCompiler(List<Diagnostic> diagnostics)
    {
        this.diagnostics = diagnostics;
        rich = new RichTextCompiler(diagnostics);
    }
```

1. Change `Compile` so lessons receive the glossary and collect keys:

```csharp
        var checks = new List<InlineCheckKey>();
        var lessons = InModuleOrder(s.Lessons.Select(l => CompileLesson(l, glossary, checks)).ToArray(), modules);
```

   and pass `checks.ToArray()` instead of `[]` for `Checks` in the `new Pack(...)` call.

2. Replace `CompileLesson` and `CompileBlock`, and add `CompileDefinition` and `CompileCheck`:

```csharp
    private Lesson CompileLesson(SourceLesson l, GlossaryEntry[] glossary, List<InlineCheckKey> checks) =>
        new(l.Id, l.Title, l.Summary, l.ObjectiveIds, l.Blocks.Select(b => CompileBlock(l, b, glossary, checks)).ToArray(),
            l.Minutes, l.Language, DirectionOf(l.Language));

    private LessonBlock CompileBlock(SourceLesson lesson, SourceBlock block, GlossaryEntry[] glossary, List<InlineCheckKey> checks)
    {
        var at = $"lessons.{lesson.Id}.blocks.{block.Id}";
        RichBlock[] Blocks(string text, string field) => rich.Blocks(text, $"{at}.{field}", lesson.Id);
        RichInline[] Inlines(string text, string field) => rich.Inlines(text, $"{at}.{field}", lesson.Id);
        return block switch
        {
            SourceTextBlock b => new TextBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Body, "body")),
            SourceCalloutBlock b => new CalloutBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Body, "body")),
            SourceExampleBlock b => new ExampleBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Body, "body")),
            SourceCodeBlock b => new CodeSampleBlock(b.Id, b.Title, b.Language, b.Code),
            SourceMathBlock b => new MathBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), b.Tex,
                TexParser.Parse(b.Tex, $"{at}.tex", diagnostics), b.Description),
            SourceTableBlock b => new TableBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), b.Caption,
                b.Columns.Select((column, i) => Inlines(column, $"columns[{i}]")).ToArray(),
                b.Rows.Select((row, r) => row.Select((cell, i) => Inlines(cell, $"rows[{r}][{i}]")).ToArray()).ToArray(), b.RowHeaders),
            SourceWorkedExampleBlock b => new WorkedExampleBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Problem, "problem"),
                b.Steps.Select((step, i) => Blocks(step, $"steps[{i}]")).ToArray(), Blocks(b.Result, "result")),
            SourceMisconceptionBlock b => new MisconceptionBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language),
                Blocks(b.Claim, "claim"), Blocks(b.Correction, "correction")),
            SourceDefinitionBlock b => CompileDefinition(lesson, b, at, glossary),
            SourcePrimarySourceBlock b => new PrimarySourceBlock(b.Id, b.Title, b.Language, DirectionOf(b.Language), Blocks(b.Excerpt, "excerpt"),
                b.Attribution, b.Translation is null ? null : Blocks(b.Translation, "translation")),
            SourceInlineCheckBlock b => CompileCheck(lesson, b, at, checks),
            _ => throw new InvalidOperationException($"Unsupported block kind in {at}.")
        };
    }

    // An unknown term is reported by RichContentRules; the block still compiles so every diagnostic surfaces at once.
    private DefinitionBlock CompileDefinition(SourceLesson lesson, SourceDefinitionBlock b, string at, GlossaryEntry[] glossary)
    {
        var entry = glossary.FirstOrDefault(g => g.Id == b.TermId);
        return new(b.Id, b.Language, DirectionOf(b.Language), b.TermId, entry?.Term ?? b.TermId, entry?.Definition ?? [],
            b.Body is null ? null : rich.Blocks(b.Body, $"{at}.body", lesson.Id));
    }

    // The key goes to Pack.Checks; the lesson keeps only the learner-facing question.
    private InlineCheckBlock CompileCheck(SourceLesson lesson, SourceInlineCheckBlock b, string at, List<InlineCheckKey> checks)
    {
        var q = b.Question;
        var question = new DeliveryQuestion(b.Id, q.Kind, rich.Blocks(q.Prompt, $"{at}.question.prompt", lesson.Id), lesson.ObjectiveIds,
            q.Options.Select(o => CompileOption(o, $"{at}.question.options.{o.Id}", lesson.Id, plain: false)).ToArray(),
            q.Slots.Select(s => CompileSlot(q.Kind, s, $"{at}.question.slots.{s.Id}", lesson.Id)).ToArray(),
            q.SelectCount, q.Reuse, null, 1);
        checks.Add(new(lesson.Id, b.Id, q.Grading, rich.Blocks(q.Explanation, $"{at}.question.explanation", lesson.Id)));
        return new(b.Id, b.Title, b.Language, DirectionOf(b.Language), question);
    }
```

- [ ] **Step 5: Validate, walk and search the new blocks**

In `RichContentRules.cs`, replace `Block` with:

```csharp
    private static void Block(Pack p, Lesson lesson, LessonBlock block, string at, HashSet<string> terms, List<Diagnostic> errors)
    {
        switch (block)
        {
            case TextBlock b: Body(b.Body, b.Language, at, errors); break;
            case CalloutBlock b: Body(b.Body, b.Language, at, errors); break;
            case ExampleBlock b: Body(b.Body, b.Language, at, errors); break;
            case CodeSampleBlock b:
                if (b.Language is null || !RichTextCompiler.CodeLanguage().IsMatch(b.Language))
                    Add(errors, "LF100", at, "Code blocks need a language label such as python, sql or text.");
                break;
            case MathBlock b:
                if (string.IsNullOrWhiteSpace(b.Description))
                    Add(errors, "A11Y_ALTERNATIVE", at, "Math blocks need a plain-language description.", "Describe what the equation says in words.");
                Language(b.Language, at + ".language", errors);
                break;
            case TableBlock b:
                if (string.IsNullOrWhiteSpace(b.Caption))
                    Add(errors, "A11Y_ALTERNATIVE", at, "Tables need a caption that states their purpose.", "Add a caption such as \"Truth values of AND and OR\".");
                if (b.Columns.Length is < 1 or > 20 || b.Rows.Length is < 1 or > 500) Add(errors, "LF100", at, "Tables need 1–20 columns and 1–500 rows.");
                if (b.Rows.Any(row => row.Length != b.Columns.Length))
                    Add(errors, "A11Y_STRUCTURE", at, "Every table row needs one cell per column.", "Merged cells are not supported. Split complex tables into simpler ones.");
                Language(b.Language, at + ".language", errors);
                break;
            case WorkedExampleBlock b:
                if (b.Steps.Length is < 1 or > 30 || RichText.IsBlank(b.Problem) || RichText.IsBlank(b.Result) || b.Steps.Any(step => RichText.IsBlank(step)))
                    Add(errors, "LF100", at, "Worked examples need a problem, 1–30 steps and a result.");
                Language(b.Language, at + ".language", errors);
                break;
            case MisconceptionBlock b:
                if (RichText.IsBlank(b.Claim) || RichText.IsBlank(b.Correction)) Add(errors, "LF100", at, "Misconceptions need a claim and a correction.");
                Language(b.Language, at + ".language", errors);
                break;
            case DefinitionBlock b:
                if (!terms.Contains(b.TermId)) Add(errors, "LF203", at, $"The glossary has no term '{b.TermId}'.", "Add the term to the glossary or fix termId.");
                Language(b.Language, at + ".language", errors);
                break;
            case PrimarySourceBlock b:
                if (RichText.IsBlank(b.Excerpt) || string.IsNullOrWhiteSpace(b.Attribution.Title))
                    Add(errors, "LF100", at, "Primary sources need an excerpt and an attribution title.");
                if (b.Attribution.Url is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
                    Add(errors, "LF202", at, "Attribution links must use HTTPS.", "Use an https:// address or remove the link.");
                if (b.Translation is not null && b.Language is null)
                    Add(errors, "A11Y_LANGUAGE", at, "A translated source needs the language of its original text.", "Set language, for example \"la\".");
                Language(b.Language, at + ".language", errors);
                break;
            case InlineCheckBlock b:
                var key = p.Checks.FirstOrDefault(k => k.LessonId == lesson.Id && k.BlockId == b.Id);
                if (key is null)
                {
                    Add(errors, "LF100", at, "This inline check has no answer key.");
                    break;
                }
                var question = InlineChecks.Build(b.Question, key);
                if (RichText.IsBlank(question.Prompt) || RichText.IsBlank(question.Explanation)) Add(errors, "LF100", at, "Inline checks need a prompt and an explanation.");
                ContentEngine.CheckQuestion(question, at, errors);
                Language(b.Language, at + ".language", errors);
                break;
        }
    }
```

In `Check`, after the dropdown loop, add the orphan-key check:

```csharp
        foreach (var k in p.Checks)
            if (!lessons.TryGetValue(k.LessonId, out var owner) || !owner.Blocks.OfType<InlineCheckBlock>().Any(b => b.Id == k.BlockId))
                Add(errors, "LF100", "checks", $"The answer key for '{k.LessonId}#{k.BlockId}' has no inline check.");
```

In `RichWalk.FromLessonBlock`, add these arms before the discard arm. The definition's glossary text is walked under `glossary.*`, not here.

```csharp
        TableBlock b => b.Columns.Concat(b.Rows.SelectMany(row => row)).SelectMany(cell => FromInlines(cell)),
        WorkedExampleBlock b => FromBlocks(b.Problem).Concat(b.Steps.SelectMany(step => FromBlocks(step))).Concat(FromBlocks(b.Result)),
        MisconceptionBlock b => FromBlocks(b.Claim).Concat(FromBlocks(b.Correction)),
        DefinitionBlock b => FromBlocks(b.Body ?? []),
        PrimarySourceBlock b => FromBlocks(b.Excerpt).Concat(FromBlocks(b.Translation ?? [])),
        InlineCheckBlock b => FromQuestion(b.Question),
```

In `RichText.PlainText(LessonBlock)`, add before the discard arm:

```csharp
        MathBlock b => b.Description,
        TableBlock b => string.Join("\n", new[] { b.Caption }.Concat(b.Columns.Concat(b.Rows.SelectMany(row => row)).Select(cell => PlainText(cell)))),
        WorkedExampleBlock b => string.Join("\n", new[] { PlainText(b.Problem) }.Concat(b.Steps.Select(step => PlainText(step))).Append(PlainText(b.Result))),
        MisconceptionBlock b => PlainText(b.Claim) + "\n" + PlainText(b.Correction),
        DefinitionBlock b => b.Term + ": " + PlainText(b.Definition) + (b.Body is null ? "" : "\n" + PlainText(b.Body)),
        PrimarySourceBlock b => PlainText(b.Excerpt) + (b.Translation is null ? "" : "\n" + PlainText(b.Translation)),
        InlineCheckBlock b => PlainText(b.Question.Prompt),
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.ContentBlocksTests"`, then `dotnet test LearnForge.slnx`.
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/LearnForge.Core tests/LearnForge.Tests/ContentBlocksTests.cs
git commit -m "feat(core): add math, table, worked example, misconception, definition, primary source and inline check blocks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Migrate the demo pack; CLI `init`, `upgrade` and `build`

**Files:**
- Modify: `packs/reasoning-foundations.json` (via the script below)
- Create: `src/LearnForge.Core/Services/Content/MarkdownText.cs`
- Modify: `tools/cli/Program.cs`
- Test: `tests/LearnForge.Tests/MarkdownTextTests.cs`, `tests/LearnForge.Tests/DemoPackTests.cs`

**Interfaces:**
- Produces: `MarkdownText.Escape(string) : string`; the demo pack `reasoning-foundations@2.0.0` (schema 2) using all 11 block kinds, with lesson, question and family IDs unchanged; CLI commands `init` (writes a v2 starter), `upgrade <v1.json> --language <tag> --out <v2.json>` and `build` (whose `grading.private.json` becomes `{ "questions": {…}, "checks": [...] }`).

- [ ] **Step 1: Write the failing tests**

`tests/LearnForge.Tests/MarkdownTextTests.cs`:

```csharp
using Xunit;

namespace LearnForge.Tests;

public class MarkdownTextTests
{
    private static RichBlock[] Compile(string markdown)
    {
        var diagnostics = new List<Diagnostic>();
        var blocks = new RichTextCompiler(diagnostics).Blocks(markdown, "f");
        Assert.Empty(diagnostics);
        return blocks;
    }

    [Theory]
    [InlineData("Use *stars*, _under_, `ticks`, [links](x), <b>, $5 & #1 | ~ \\ !")]
    [InlineData("- not a list")]
    [InlineData("1. not a list")]
    [InlineData("# not a heading")]
    [InlineData("> not a quote")]
    [InlineData("    indented")]
    public void Escaped_text_compiles_back_to_the_same_text(string text) =>
        Assert.Equal(text.TrimStart(), RichText.PlainText(Compile(MarkdownText.Escape(text))));

    [Fact] public void Line_breaks_become_hard_breaks_and_blank_lines_new_paragraphs()
    {
        var blocks = Compile(MarkdownText.Escape("one\ntwo\n\n\nthree"));
        Assert.Equal(2, blocks.Length);
        Assert.Equal("one\ntwo", RichText.PlainText(blocks[0]));
    }
}
```

`tests/LearnForge.Tests/DemoPackTests.cs`:

```csharp
using Xunit;

namespace LearnForge.Tests;

public class DemoPackTests
{
    [Fact] public void The_demo_pack_exercises_every_block_kind()
    {
        var pack = CoreTests.Demo();
        Assert.Equal(2, pack.SchemaVersion);
        Assert.Equal("2.0.0", pack.Version);
        Assert.Equal("en", pack.Language);
        Assert.Equal(11, pack.Lessons.SelectMany(l => l.Blocks).Select(b => b.GetType()).Distinct().Count());
        Assert.Equal(new[] { "sets-intro", "logic-intro", "ordering-intro" }, pack.Lessons.Select(l => l.Id));
        Assert.Equal(2, pack.Checks.Length);
        Assert.Equal(5, pack.Glossary.Length);
        Assert.Equal(40, pack.Questions.Length);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.MarkdownTextTests|FullyQualifiedName~LearnForge.Tests.DemoPackTests"`
Expected: build FAIL, `MarkdownText` not found.

- [ ] **Step 3: Implement the escaper**

`src/LearnForge.Core/Services/Content/MarkdownText.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace LearnForge.Core;

// Escapes schema 1 literal text so that Markdown renders it unchanged: inline syntax characters get a backslash,
// line-start block markers are escaped, single line breaks become hard breaks and blank lines separate paragraphs.
public static partial class MarkdownText
{
    public static string Escape(string text) => string.Join("\n\n",
        ParagraphBreak().Split(text.Replace("\r\n", "\n")).Select(paragraph => string.Join("\\\n", paragraph.Split('\n').Select(EscapeLine))));

    // Leading indentation is dropped: in Markdown it would turn the line into a code block.
    private static string EscapeLine(string line)
    {
        var escaped = new StringBuilder();
        foreach (var c in line.TrimStart())
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '$' or '&' or '|' or '~' or '#' or '!') escaped.Append('\\');
            escaped.Append(c);
        }
        var result = escaped.ToString();
        var marker = BlockMarker().Match(result);
        return marker.Success ? result.Insert(marker.Groups["mark"].Index, "\\") : result;
    }

    [GeneratedRegex(@"\n{2,}")]
    private static partial Regex ParagraphBreak();

    // A leading -, + or = (list or setext underline), or a number followed by . or ), would start a block.
    [GeneratedRegex(@"^(?:(?<mark>[-+=])|\d+(?<mark>[.)]))")]
    private static partial Regex BlockMarker();
}
```

- [ ] **Step 4: Migrate the demo pack**

Run this script from the repository root. It keeps the templates and all 40 questions (question and family IDs unchanged; dropdown blank labels become math), and replaces pack metadata and lessons. Question text needs no escaping: it contains no Markdown-significant characters (checked with a regex scan during planning).

```bash
python3 - <<'PY'
import json
path = 'packs/reasoning-foundations.json'
doc = json.load(open(path, encoding='utf-8'))
p = doc['pack']
lessons = [
  {"id": "sets-intro", "title": "Think in sets", "summary": "Describe collections, membership and overlap.",
   "objectiveIds": ["sets"], "minutes": 10, "blocks": [
    {"id": "what-is-a-set", "kind": "text", "body": "A [set](term:set) is a collection of distinct elements. Order does not matter: $\\{1, 2\\}$ and $\\{2, 1\\}$ describe the same set."},
    {"id": "union-definition", "kind": "definition", "termId": "union"},
    {"id": "union-and-intersection", "kind": "workedExample", "title": "Union and intersection",
     "problem": "For $A = \\{1, 2\\}$ and $B = \\{2, 3\\}$, find $A \\cup B$ and $A \\cap B$.",
     "steps": ["The [union](term:union) collects every element that belongs to either set: $1$, $2$ and $3$.",
               "The [intersection](term:intersection) keeps the elements that belong to both sets. Only $2$ belongs to both."],
     "result": "$A \\cup B = \\{1, 2, 3\\}$ and $A \\cap B = \\{2\\}$."},
    {"id": "union-means-either", "kind": "misconception",
     "claim": "The union contains only the elements that appear in *both* sets.",
     "correction": "Union means **either** set; elements in both sets form the [intersection](term:intersection). Drawing two overlapping circles can help you separate the ideas."},
    {"id": "set-sizes", "kind": "table", "caption": "Two sets and their combinations", "columns": ["Expression", "Elements", "Size"],
     "rows": [["$A$", "$\\{1, 2\\}$", "2"], ["$B$", "$\\{2, 3\\}$", "2"], ["$A \\cup B$", "$\\{1, 2, 3\\}$", "3"], ["$A \\cap B$", "$\\{2\\}$", "1"]],
     "rowHeaders": True},
    {"id": "check-intersection", "kind": "inlineCheck", "title": "Check your understanding", "question": {
      "kind": "single", "prompt": "Which set is $\\{1, 2\\} \\cap \\{2, 3\\}$?",
      "options": [{"id": "a", "text": "$\\{2\\}$"}, {"id": "b", "text": "$\\{1, 2, 3\\}$"}, {"id": "c", "text": "$\\emptyset$"}],
      "slots": [], "selectCount": 1, "reuse": False, "grading": {"policy": "exact", "correct": ["a"], "matches": None},
      "explanation": "The [intersection](term:intersection) keeps elements that belong to both sets, and only $2$ appears in both."}}]},
  {"id": "logic-intro", "title": "Build logical statements", "summary": "Evaluate AND and OR using truth values.",
   "objectiveIds": ["logic"], "minutes": 12, "blocks": [
    {"id": "truth-values", "kind": "text", "body": "A logical statement has a truth value: true or false. [Conjunction](term:conjunction) (AND) is true only when both operands are true. Inclusive [disjunction](term:disjunction) (OR) is true when at least one operand is true."},
    {"id": "truth-table", "kind": "table", "caption": "Truth values of AND and OR", "columns": ["$P$", "$Q$", "$P \\land Q$", "$P \\lor Q$"],
     "rows": [["true", "true", "true", "true"], ["true", "false", "false", "true"], ["false", "true", "false", "true"], ["false", "false", "false", "false"]]},
    {"id": "one-step", "kind": "example", "title": "Evaluate one step at a time", "body": "Let $P$ be true and $Q$ be false. $P \\land Q$ is false. $P \\lor Q$ is true. Write down the input values before applying the operator."},
    {"id": "de-morgan", "kind": "math", "title": "De Morgan's law", "tex": "\\neg (P \\land Q) \\equiv \\neg P \\lor \\neg Q",
     "description": "Not (P and Q) is equivalent to (not P) or (not Q)."},
    {"id": "inclusive-or", "kind": "callout", "title": "Check your interpretation", "body": "In this course OR is inclusive: both inputs may be true. Everyday language sometimes uses OR to mean exactly one; mathematics distinguishes this as exclusive OR. Compare AND with [union and intersection](lesson:sets-intro#union-and-intersection)."},
    {"id": "laws-of-thought", "kind": "primarySource", "title": "Logic as a calculus",
     "excerpt": "The design of the following treatise is to investigate the fundamental laws of those operations of the mind by which reasoning is performed; to give expression to them in the symbolical language of a Calculus, and upon this foundation to establish the science of Logic and construct its method; …",
     "attribution": {"title": "An Investigation of the Laws of Thought", "author": "George Boole", "date": "1854"}},
    {"id": "modus-ponens", "kind": "text", "body": "A rule of inference such as [modus ponens](lang:la) lets you move from true statements to a new true statement."},
    {"id": "check-and-or", "kind": "inlineCheck", "question": {
      "kind": "dropdown", "prompt": "Let $P$ be true and $Q$ be false. Complete both evaluations.", "options": [],
      "slots": [{"id": "and", "text": "$P \\land Q$", "options": [{"id": "true", "text": "True"}, {"id": "false", "text": "False"}]},
                {"id": "or", "text": "$P \\lor Q$", "options": [{"id": "true", "text": "True"}, {"id": "false", "text": "False"}]}],
      "selectCount": 1, "reuse": False, "grading": {"policy": "partial", "correct": [], "matches": {"and": "false", "or": "true"}},
      "explanation": "AND needs both inputs to be true, so $P \\land Q$ is false. OR needs at least one, so $P \\lor Q$ is true."}}]},
  {"id": "ordering-intro", "title": "Reason in sequence", "summary": "Follow a procedure and check its invariant.",
   "objectiveIds": ["ordering"], "minutes": 10, "blocks": [
    {"id": "order-matters", "kind": "text", "body": "A sequence records order. When sorting numbers increasingly, every adjacent pair must satisfy $\\text{left} \\le \\text{right}$."},
    {"id": "sort-small-list", "kind": "workedExample", "title": "Sort a small list", "problem": "Sort $8, 3, 6, 1$ in increasing order.",
     "steps": ["Choose the smallest remaining value repeatedly: $1$, then $3$, then $6$, then $8$.", "Check that each value appears exactly once."],
     "result": "$1, 3, 6, 8$"},
    {"id": "invariant-check", "kind": "code", "title": "A check in pseudocode", "language": "pseudocode",
     "code": "for each adjacent pair (left, right):\n    require left <= right\nrequire output has the same elements as input"}]}]
glossary = [
  {"id": "set", "term": "set", "definition": "A collection of distinct elements, where order does not matter."},
  {"id": "union", "term": "union", "definition": "The set of elements that belong to *either* set, written $A \\cup B$."},
  {"id": "intersection", "term": "intersection", "definition": "The set of elements that belong to *both* sets, written $A \\cap B$."},
  {"id": "conjunction", "term": "conjunction", "definition": "A statement joined with AND, written $P \\land Q$; true only when both parts are true."},
  {"id": "disjunction", "term": "disjunction", "definition": "A statement joined with OR, written $P \\lor Q$; true when at least one part is true."}]
# Dropdown blanks get math labels; every short session includes a dropdown, so attempts always show math.
for q in p["questions"]:
    if q["values"]["kind"] == "dropdown":
        for slot in q["values"]["slots"]:
            slot["text"] = {"P AND Q": "$P \\land Q$", "P OR Q": "$P \\lor Q$"}.get(slot["text"], slot["text"])
pack = {
  "schemaVersion": 2, "id": p["id"], "version": "2.0.0", "title": p["title"], "description": p["description"], "license": p["license"],
  "language": "en",
  "catalog": {"subject": "Mathematics", "level": "introductory", "tags": ["sets", "logic", "reasoning"], "estimatedHours": 2},
  "objectives": p["objectives"],
  "modules": [{"id": "foundations", "title": "Sets and logic", "lessonIds": ["sets-intro", "logic-intro"]},
              {"id": "procedures", "title": "Procedures", "lessonIds": ["ordering-intro"]}],
  "glossary": glossary, "lessons": lessons, "questions": p["questions"], "scenarios": p["scenarios"],
  "blueprints": p["blueprints"], "readiness": p["readiness"], "sources": p["sources"]}
doc["pack"] = pack
with open(path, 'w', encoding='utf-8') as f:
    json.dump(doc, f, ensure_ascii=False, indent=2)
    f.write('\n')
PY
```

Run: `make check-content`
Expected: `PASS reasoning-foundations@2.0.0: 3 lessons, 40 questions, 3 objectives.` and `PASS evidence-lab@…`.

- [ ] **Step 5: Update the CLI**

In `tools/cli/Program.cs`:

1. Update the help line:

```csharp
    Console.WriteLine("LearnForge content compiler\n  check <source.json> [--watch] [--json]\n  build <source.json> --out <directory>\n  diff <before.json> <after.json>\n  init <source.json>\n  upgrade <v1.json> --language <tag> --out <v2.json>\nTemplates use $use + values; see docs/authoring.md.");
```

2. Replace the `init` branch body (after the existence check) with a schema 2 starter:

```csharp
        const string starter = """
        {
          "schemaVersion": 2,
          "id": "my-course",
          "version": "1.0.0",
          "title": "My first course",
          "description": "Replace this sample with your subject.",
          "license": "Private",
          "language": "en",
          "catalog": { "subject": "Mathematics", "level": "introductory", "tags": ["numbers"], "estimatedHours": 1 },
          "objectives": [{ "id": "parity", "title": "Recognize even integers", "prerequisites": [] }],
          "glossary": [{ "id": "even", "term": "even", "definition": "Divisible by two, such as $4$ or $-2$." }],
          "lessons": [{
            "id": "introduction", "title": "Even integers", "summary": "A short introduction.", "objectiveIds": ["parity"], "minutes": 5,
            "blocks": [
              { "id": "idea", "kind": "text", "body": "An integer is [even](term:even) when it is divisible by two: $n = 2k$ for some integer $k$." },
              { "id": "check-even", "kind": "inlineCheck", "question": { "kind": "single", "prompt": "Is $6$ even?",
                "options": [{ "id": "yes", "text": "Yes" }, { "id": "no", "text": "No" }], "slots": [], "selectCount": 1, "reuse": false,
                "grading": { "policy": "exact", "correct": ["yes"], "matches": null }, "explanation": "$6 = 2 \\times 3$." } }
            ]
          }],
          "questions": [{
            "id": "starter-q", "familyId": "starter-family", "kind": "single", "prompt": "Which value is even?", "objectiveIds": ["parity"],
            "options": [{ "id": "a", "text": "2" }, { "id": "b", "text": "3" }], "slots": [], "selectCount": 1, "reuse": false,
            "grading": { "policy": "exact", "correct": ["a"], "matches": null }, "explanation": "An even integer is divisible by two."
          }],
          "blueprints": [{ "id": "short", "title": "Quick check", "count": 1, "minutes": 5, "size": "short", "objectiveIds": ["parity"], "requiredKinds": ["single"], "lockSections": false }],
          "readiness": { "shortAttempts": 5, "fullAttempts": 3, "threshold": 90, "lookbackDays": 90, "minimumFreshPercent": 100 }
        }
        """;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, starter);
        Console.WriteLine($"Created {path}"); return 0;
```

3. After the `diff` branch, add `upgrade`:

```csharp
    if (command == "upgrade")
    {
        var languageIndex = Array.IndexOf(args, "--language");
        var upgradeIndex = Array.IndexOf(args, "--out");
        if (languageIndex < 0 || languageIndex + 1 >= args.Length || upgradeIndex < 0 || upgradeIndex + 1 >= args.Length)
            throw new ArgumentException("upgrade requires --language <tag> and --out <v2.json>.");
        var language = args[languageIndex + 1];
        if (!LanguageTag.IsValid(language)) throw new ArgumentException($"'{language}' is not a valid language tag.");
        if (p.SchemaVersion != 1) throw new ArgumentException("upgrade converts schema 1 packs; this pack already uses schema 2.");
        var target = Path.GetFullPath(args[upgradeIndex + 1]);
        if (File.Exists(target)) throw new ArgumentException("Destination already exists.");
        string Md(RichBlock[] blocks) => MarkdownText.Escape(RichText.PlainText(blocks));
        string MdInline(RichInline[] inlines) => MarkdownText.Escape(RichText.PlainText(inlines));
        object UpgradeBlock(LessonBlock block) => block switch
        {
            CodeSampleBlock b => new { b.Id, Kind = "code", b.Title, Language = "text", b.Code },
            CalloutBlock b => new { b.Id, Kind = "callout", b.Title, Body = Md(b.Body) },
            ExampleBlock b => new { b.Id, Kind = "example", b.Title, Body = Md(b.Body) },
            TextBlock b => new { b.Id, Kind = "text", b.Title, Body = Md(b.Body) },
            _ => throw new InvalidOperationException("Schema 1 packs contain only text, callout, example and code blocks.")
        };
        var upgraded = new
        {
            SchemaVersion = 2, p.Id, p.Version, p.Title, p.Description, p.License, Language = language,
            Direction = LanguageTag.DirectionOf(language),
            Catalog = new { Subject = "General", Level = CourseLevel.Introductory, Tags = Array.Empty<string>() },
            p.Objectives,
            Lessons = p.Lessons.Select(l => new { l.Id, l.Title, l.Summary, l.ObjectiveIds, Blocks = l.Blocks.Select(UpgradeBlock) }),
            Questions = p.Questions.Select(q => new
            {
                q.Id, q.FamilyId, q.Kind, Prompt = Md(q.Prompt), q.ObjectiveIds,
                Options = q.Options.Select(o => new { o.Id, Text = MdInline(o.Text) }),
                Slots = q.Slots.Select(s => new { s.Id, Text = MdInline(s.Text), Options = s.Options.Select(o => new { o.Id, Text = MdInline(o.Text) }) }),
                q.SelectCount, q.Reuse, q.Grading, Explanation = Md(q.Explanation), q.ScenarioId, q.Weight
            }),
            Scenarios = p.Scenarios.Select(s => new { s.Id, s.Title, Background = Md(s.Background) }),
            p.Blueprints, p.Sources, p.Readiness, p.Goal, p.Mastery
        };
        var text = Json.Write(upgraded);
        Report(ContentEngine.Compile(text));
        await File.WriteAllTextAsync(target, text);
        if (System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))?["templates"] is not null)
            Console.WriteLine("Note: templates were expanded inline in the upgraded pack.");
        Console.WriteLine($"Wrote {target}. Review the catalog stub (subject \"General\") and add block titles or new blocks as needed.");
        return 0;
    }
```

4. In `build`, replace the `grading.private.json` artifact:

```csharp
        ["grading.private.json"] = Json.Write(new { Questions = p.Questions.ToDictionary(q => q.Id, q => new { q.Grading, q.Explanation }), p.Checks }),
```

- [ ] **Step 6: Verify the CLI end to end**

```bash
tmp="$(mktemp -d)"
dotnet run --project tools/cli -- init "$tmp/starter.json" && dotnet run --project tools/cli -- check "$tmp/starter.json"
dotnet run --project tools/cli -- upgrade packs/evidence-lab.json --language en --out "$tmp/evidence-v2.json"
dotnet run --project tools/cli -- check "$tmp/evidence-v2.json"
dotnet run --project tools/cli -- build packs/reasoning-foundations.json --out "$tmp/build" && grep -c '"blockId"' "$tmp/build/grading.private.json"
```

Expected: `PASS my-course@1.0.0 …`; the upgrade prints `PASS evidence-lab@…` and `Wrote …`; the second check prints `PASS`; `grep -c` prints `2`.

- [ ] **Step 7: Run all tests**

Run: `dotnet test LearnForge.slnx`
Expected: PASS, including `DemoPackTests`, `MarkdownTextTests` and every existing test that uses the demo pack (`CoreTests`, `GoalPolicyTests`, `NextStepPlannerTests`, `LearningRecordTests`).

- [ ] **Step 8: Commit**

```bash
git add packs/reasoning-foundations.json src/LearnForge.Core tools/cli tests/LearnForge.Tests
git commit -m "feat: migrate the demo pack to schema 2 and add CLI init, upgrade and check keys

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Catalog metadata and the inline-check endpoint

**Files:**
- Modify: `apps/api/Contracts/Content/CatalogSummaryDto.cs`, `CourseCatalogDto.cs`
- Create: `apps/api/Contracts/Content/InlineCheckRequest.cs`, `InlineCheckResultDto.cs`
- Modify: `apps/api/Endpoints/CatalogEndpoints.cs`, `apps/api/Program.cs`
- Test: `tests/LearnForge.Tests/CatalogContentTests.cs`, `tests/LearnForge.Tests/InlineCheckRateLimitTests.cs`

**Interfaces:**
- Consumes: `InlineChecks.Question` (Task 9), `Pack` metadata (Task 6).
- Produces:
  - `CatalogSummaryDto(Id, Title, Description, Version, LessonCount, QuestionCount, ObjectiveCount, string? Language, TextDirection Direction, string? Subject, CourseLevel? Level, string[] Tags, decimal? EstimatedHours)` with `static From(Pack)`.
  - `CourseCatalogDto(Id, Title, Description, Version, License, string? Language, TextDirection Direction, CatalogMetadata? Catalog, Objective[] Objectives, Module[] Modules, GlossaryEntry[] Glossary, Lesson[] Lessons, Blueprint[] Blueprints, SourceReference[] Sources, ReadinessPolicy Readiness, CourseGoal Goal, int QuestionCount)` with `static From(Pack)`.
  - `POST /api/catalog/{packId}/checks` taking `InlineCheckRequest(string Version, string LessonId, string BlockId, Answer Answer)` and returning `InlineCheckResultDto(Grade Grade, RichBlock[] Explanation)`. Rate-limit policy `checks`: 60 per minute per user or IP. HTTP JSON `MaxDepth = 128`.

- [ ] **Step 1: Write the failing tests**

`tests/LearnForge.Tests/CatalogContentTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnForge.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LearnForge.Tests;

public class CatalogContentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] PrivateNames = ["grading", "correct", "matches", "explanation", "checks", "lessonHashes"];

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => PropertyNames(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(e => PropertyNames(e)),
        _ => Enumerable.Empty<string>()
    };

    private async Task<HttpClient> Anonymous()
    {
        var client = factory.CreateClient(new() { HandleCookies = true });
        await TestApi.Token(client);
        return client;
    }

    private static async Task<string> Version(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/catalog/reasoning-foundations")).GetProperty("version").GetString()!;

    private static Task<HttpResponseMessage> Check(HttpClient client, string version, string lessonId, string blockId, object answer) =>
        client.PostAsJsonAsync("/api/catalog/reasoning-foundations/checks", new { version, lessonId, blockId, answer });

    private static readonly object ChooseA = new { selected = new[] { "a" }, slots = new { } };

    [Fact] public async Task Catalog_summaries_carry_metadata()
    {
        var courses = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/catalog");
        var demo = courses.EnumerateArray().Single(c => c.GetProperty("id").GetString() == "reasoning-foundations");
        Assert.Equal("Mathematics", demo.GetProperty("subject").GetString());
        Assert.Equal("introductory", demo.GetProperty("level").GetString());
        Assert.Equal("en", demo.GetProperty("language").GetString());
        Assert.Equal(3, demo.GetProperty("tags").GetArrayLength());
        var evidence = courses.EnumerateArray().Single(c => c.GetProperty("id").GetString() == "evidence-lab");
        Assert.Equal(JsonValueKind.Null, evidence.GetProperty("language").ValueKind);
        Assert.Equal(0, evidence.GetProperty("tags").GetArrayLength());
    }

    [Fact] public async Task Course_catalog_includes_modules_glossary_and_rich_lessons()
    {
        var course = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/catalog/reasoning-foundations");
        Assert.Equal(2, course.GetProperty("modules").GetArrayLength());
        Assert.Equal(5, course.GetProperty("glossary").GetArrayLength());
        Assert.Equal("ltr", course.GetProperty("direction").GetString());
        var blocks = course.GetProperty("lessons")[0].GetProperty("blocks");
        Assert.Equal("text", blocks[0].GetProperty("kind").GetString());
        Assert.Contains(blocks.EnumerateArray(), b => b.GetProperty("kind").GetString() == "inlineCheck");
    }

    [Theory]
    [InlineData("reasoning-foundations")]
    [InlineData("evidence-lab")]
    public async Task Catalog_responses_never_contain_answer_keys(string packId)
    {
        var client = factory.CreateClient();
        Assert.Empty(PropertyNames(await client.GetFromJsonAsync<JsonElement>($"/api/catalog/{packId}")).Intersect(PrivateNames));
        Assert.Empty(PropertyNames(await client.GetFromJsonAsync<JsonElement>("/api/catalog")).Intersect(PrivateNames));
    }

    [Fact] public async Task Inline_checks_grade_on_the_server_without_signing_in()
    {
        var client = await Anonymous();
        var version = await Version(client);
        var right = await (await Check(client, version, "sets-intro", "check-intersection", ChooseA)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(right.GetProperty("grade").GetProperty("fullyCorrect").GetBoolean());
        Assert.Equal("paragraph", right.GetProperty("explanation")[0].GetProperty("kind").GetString());
        var wrong = await (await Check(client, version, "sets-intro", "check-intersection", new { selected = new[] { "b" }, slots = new { } }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(wrong.GetProperty("grade").GetProperty("fullyCorrect").GetBoolean());
        var partial = await (await Check(client, version, "logic-intro", "check-and-or", new { selected = Array.Empty<string>(), slots = new { and = "false", or = "false" } }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.5m, partial.GetProperty("grade").GetProperty("earned").GetDecimal());
    }

    [Fact] public async Task Inline_check_errors_are_client_errors()
    {
        var client = await Anonymous();
        var version = await Version(client);
        Assert.Equal(HttpStatusCode.NotFound, (await Check(client, "9.9.9", "sets-intro", "check-intersection", ChooseA)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Check(client, version, "sets-intro", "missing", ChooseA)).StatusCode);
        // A real block that is not an inline check must not be gradable.
        Assert.Equal(HttpStatusCode.NotFound, (await Check(client, version, "sets-intro", "what-is-a-set", ChooseA)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Check(client, version, "sets-intro", "check-intersection", new { selected = new[] { "zzz" }, slots = new { } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/catalog/reasoning-foundations/checks", new { version, lessonId = "sets-intro", blockId = "check-intersection" })).StatusCode);
    }

    [Fact] public async Task Signed_in_checks_write_no_evidence_or_attempts()
    {
        var client = await TestApi.Account(factory);
        (await Check(client, await Version(client), "sets-intro", "check-intersection", ChooseA)).EnsureSuccessStatusCode();
        var userId = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.Evidence.AnyAsync(e => e.UserId == userId));
        Assert.False(await db.Attempts.AnyAsync(a => a.UserId == userId));
    }
}
```

`tests/LearnForge.Tests/InlineCheckRateLimitTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

// Its own factory, so the rate-limit window is not shared with other test classes.
public class InlineCheckRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact] public async Task Inline_checks_are_rate_limited_per_caller()
    {
        var client = factory.CreateClient(new() { HandleCookies = true });
        await TestApi.Token(client);
        var version = (await client.GetFromJsonAsync<JsonElement>("/api/catalog/reasoning-foundations")).GetProperty("version").GetString();
        var request = new { version, lessonId = "sets-intro", blockId = "check-intersection", answer = new { selected = new[] { "a" }, slots = new { } } };
        for (var i = 0; i < 60; i++) (await client.PostAsJsonAsync("/api/catalog/reasoning-foundations/checks", request)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/catalog/reasoning-foundations/checks", request)).StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.CatalogContentTests|FullyQualifiedName~LearnForge.Tests.InlineCheckRateLimitTests"`
Expected: FAIL (`subject` property missing; the checks endpoint returns 404 or 405).

- [ ] **Step 3: Implement the contracts**

`CatalogSummaryDto.cs`:

```csharp
namespace LearnForge.Api.Contracts.Content;

// Language and catalog metadata are null or empty for schema 1 packs.
public sealed record CatalogSummaryDto(string Id, string Title, string Description, string Version,
    int LessonCount, int QuestionCount, int ObjectiveCount, string? Language, TextDirection Direction,
    string? Subject, CourseLevel? Level, string[] Tags, decimal? EstimatedHours)
{
    public static CatalogSummaryDto From(Pack p) => new(p.Id, p.Title, p.Description, p.Version, p.Lessons.Length,
        p.Questions.Length, p.Objectives.Length, p.Language, p.Direction, p.Catalog?.Subject, p.Catalog?.Level,
        p.Catalog?.Tags ?? [], p.Catalog?.EstimatedHours);
}
```

`CourseCatalogDto.cs`:

```csharp
namespace LearnForge.Api.Contracts.Content;

// Public course data only; personal progress lives under /api/me/courses/{id}. Lessons never hold answer keys:
// inline-check keys live in Pack.Checks.
public sealed record CourseCatalogDto(string Id, string Title, string Description, string Version, string License,
    string? Language, TextDirection Direction, CatalogMetadata? Catalog, Objective[] Objectives, Module[] Modules,
    GlossaryEntry[] Glossary, Lesson[] Lessons, Blueprint[] Blueprints, SourceReference[] Sources,
    ReadinessPolicy Readiness, CourseGoal Goal, int QuestionCount)
{
    public static CourseCatalogDto From(Pack p) => new(p.Id, p.Title, p.Description, p.Version, p.License, p.Language,
        p.Direction, p.Catalog, p.Objectives, p.Modules, p.Glossary, p.Lessons, p.Blueprints, p.Sources,
        p.Readiness ?? new(), p.Goal, p.Questions.Length);
}
```

`InlineCheckRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Content;

// Version pins the release the learner read, so a later publish cannot change how the answer is graded.
public sealed record InlineCheckRequest(
    [property: Required, MaxLength(40)] string Version,
    [property: Required, MaxLength(100)] string LessonId,
    [property: Required, MaxLength(100)] string BlockId,
    [property: Required] Answer Answer);
```

`InlineCheckResultDto.cs`:

```csharp
namespace LearnForge.Api.Contracts.Content;

public sealed record InlineCheckResultDto(Grade Grade, RichBlock[] Explanation);
```

- [ ] **Step 4: Implement the endpoints**

Replace the body of `MapCatalog` in `CatalogEndpoints.cs`:

```csharp
            app.MapGet("/api/catalog", async (AppDb db, ReleaseCache releases) =>
            {
                var courses = new List<CatalogSummaryDto>();
                foreach (var releaseId in await releases.LatestIds(db)) courses.Add(CatalogSummaryDto.From((await releases.Get(releaseId)).Pack));
                return courses.ToArray();
            });
            app.MapGet("/api/catalog/{id}", async Task<Results<Ok<CourseCatalogDto>, NotFound>> (string id, AppDb db, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                return release is null ? TypedResults.NotFound() : TypedResults.Ok(CourseCatalogDto.From(release.Pack));
            });
            // Formative checks inside lessons: graded on the server, open to signed-out readers, never recorded as evidence.
            app.MapPost("/api/catalog/{packId}/checks", async Task<Results<Ok<InlineCheckResultDto>, NotFound>> (string packId,
                InlineCheckRequest request, AppDb db, ReleaseCache releases) =>
            {
                var releaseId = await db.Packs.Where(p => p.PackId == packId && p.Version == request.Version).Select(p => p.Id).FirstOrDefaultAsync();
                if (releaseId is null) return TypedResults.NotFound();
                var question = InlineChecks.Question((await releases.Get(releaseId)).Pack, request.LessonId, request.BlockId);
                if (question is null) return TypedResults.NotFound();
                if (Grader.Validate(question, request.Answer) is { } error) throw new DomainError(400, error);
                return TypedResults.Ok(new InlineCheckResultDto(Grader.Score(question, request.Answer), question.Explanation));
            }).RequireRateLimiting("checks");
```

In `Program.cs`, add to `ConfigureHttpJsonOptions`:

```csharp
    // Rich-text ASTs nest; the compiler bounds nesting so every response fits.
    o.SerializerOptions.MaxDepth = 128;
```

and to `AddRateLimiter`, after the `auth` policy:

```csharp
    // Inline checks are open to signed-out readers, so they have a tighter limit per user or address.
    o.AddPolicy("checks", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: PASS. The existing `The_public_catalog_contains_no_personal_progress` test and the e2e check that `body.questions` is undefined remain valid, because `CourseCatalogDto` has no `questions` property.

- [ ] **Step 6: Commit**

```bash
git add apps/api tests/LearnForge.Tests
git commit -m "feat(api): serve catalog metadata and grade inline checks on the server

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Studio preview endpoint

**Files:**
- Create: `apps/api/Contracts/Content/PreviewDto.cs`, `PreviewQuestion.cs`, `PreviewCheck.cs`
- Modify: `apps/api/Endpoints/AuthoringEndpoints.cs`
- Test: `tests/LearnForge.Tests/PreviewTests.cs`

**Interfaces:**
- Consumes: `CourseCatalogDto.From` (Task 11).
- Produces: `POST /api/authoring/preview` (Publisher role) → `PreviewDto(CourseCatalogDto Course, PreviewQuestion[] Questions, PreviewCheck[] Checks)`, where `PreviewQuestion(DeliveryQuestion Question, Grading Grading, RichBlock[] Explanation)` and `PreviewCheck(string LessonId, string BlockId, Grading Grading, RichBlock[] Explanation)`. Invalid sources → 400 with `ValidationResponseDto`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace LearnForge.Tests;

public class PreviewTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static StringContent Source(string text) => new(text, Encoding.UTF8, "application/json");

    [Fact] public async Task Only_publishers_can_preview()
    {
        var learner = await TestApi.Account(factory);
        var response = await learner.PostAsync("/api/authoring/preview", Source(File.ReadAllText(TestPacks.PackFile("reasoning-foundations"))));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] public async Task Preview_renders_the_learner_view_and_separates_answer_keys()
    {
        var publisher = await TestApi.Publisher(factory);
        var response = await publisher.PostAsync("/api/authoring/preview", Source(File.ReadAllText(TestPacks.PackFile("reasoning-foundations"))));
        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, preview.GetProperty("course").GetProperty("lessons").GetArrayLength());
        Assert.Equal(40, preview.GetProperty("questions").GetArrayLength());
        var first = preview.GetProperty("questions")[0];
        Assert.True(first.TryGetProperty("grading", out _));
        Assert.False(first.GetProperty("question").TryGetProperty("grading", out _));
        Assert.Equal(2, preview.GetProperty("checks").GetArrayLength());
    }

    [Fact] public async Task Invalid_sources_return_diagnostics_with_guidance()
    {
        var publisher = await TestApi.Publisher(factory);
        var pack = TestPacks.MinimalV2();
        TestPacks.Blocks(pack)[0]!["body"] = "# Heading";
        var response = await publisher.PostAsync("/api/authoring/preview", Source(pack.ToJsonString()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var diagnostic = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("diagnostics")[0];
        Assert.Equal("A11Y_STRUCTURE", diagnostic.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(diagnostic.GetProperty("guidance").GetString()));
        var validation = await (await publisher.PostAsync("/api/authoring/validate", Source(pack.ToJsonString()))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("lessons.intro.blocks.what.body:1:1", validation.GetProperty("diagnostics")[0].GetProperty("path").GetString());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test LearnForge.slnx --filter "FullyQualifiedName~LearnForge.Tests.PreviewTests"`
Expected: FAIL (the preview route returns 404 or 405).

- [ ] **Step 3: Implement**

```csharp
namespace LearnForge.Api.Contracts.Content;

// Author-only preview: the learner view plus answer keys, which Studio shows in a separate panel.
public sealed record PreviewDto(CourseCatalogDto Course, PreviewQuestion[] Questions, PreviewCheck[] Checks);
```
```csharp
namespace LearnForge.Api.Contracts.Content;

public sealed record PreviewQuestion(DeliveryQuestion Question, Grading Grading, RichBlock[] Explanation);
```
```csharp
namespace LearnForge.Api.Contracts.Content;

public sealed record PreviewCheck(string LessonId, string BlockId, Grading Grading, RichBlock[] Explanation);
```

In `AuthoringEndpoints.cs`, add `using Microsoft.AspNetCore.Http.HttpResults;` and, after `/validate`:

```csharp
            authoring.MapPost("/preview", Results<Ok<PreviewDto>, BadRequest<ValidationResponseDto>> (JsonElement source) =>
            {
                var result = ContentEngine.Compile(source.GetRawText());
                if (!result.Success) return TypedResults.BadRequest(new ValidationResponseDto(false, result.Hash, result.Diagnostics, null, null));
                var p = result.Pack!;
                return TypedResults.Ok(new PreviewDto(CourseCatalogDto.From(p),
                    p.Questions.Select(q => new PreviewQuestion(DeliveryQuestion.From(q), q.Grading, q.Explanation)).ToArray(),
                    p.Checks.Select(c => new PreviewCheck(c.LessonId, c.BlockId, c.Grading, c.Explanation)).ToArray()));
            });
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test LearnForge.slnx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api tests/LearnForge.Tests/PreviewTests.cs
git commit -m "feat(api): preview packs as learners see them, with answer keys kept separate

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Web renderers, question input and the attempt player

This task regenerates the API types, so every page that renders content must compile against the AST in the same task.

**Files:**
- Regenerate: `apps/web/openapi/learnforge.json`, `apps/web/src/app/generated/*` (via `make api-types`)
- Modify: `apps/web/src/app/models.ts`
- Create: `apps/web/src/app/rich/context.ts`, `ids.ts`, `plain-text.ts`, `math.ts`, `code.ts`, `inlines.ts`, `blocks.ts`, `lesson-block.ts`, `inline-check.ts`
- Create tests: `apps/web/src/app/rich/rich.spec.ts`, `lesson-block.spec.ts`, `inline-check.spec.ts`, `apps/web/src/app/question-input.spec.ts`
- Modify: `apps/web/src/app/question-input.ts`, `apps/web/src/app/pages/attempt.ts`, `apps/web/src/app/pages/course.ts`, `apps/web/src/styles.scss`

**Interfaces:**
- Consumes: the generated unions `RichBlock`, `RichInline`, `MathNode`, `LessonBlock`, plus `FeedbackDto`, `InlineCheckResultDto`, `GlossaryEntry` and `Attribution`.
- Produces:
  - `RichContext` (injectable; signals `packId`, `version`, `glossary`, `interactive`).
  - `uniqueId(prefix)`, `plainMath(node)`, `plainInlines(nodes)`, `plainBlocks(blocks)`. Math reads as Unicode text, e.g. `P ∧ Q`.
  - Components: `lf-inlines [nodes]`, `lf-blocks [blocks]`, `lf-math [root] [display] [describedBy]`, `lf-code [language] [code]`, `lf-lesson-block [block] [lessonId] [parentLanguage]`, `lf-inline-check [block] [lessonId]`.
  - `lf-question-input` gains required `instanceId` and optional `promptId`.
  - `BlockOf<K>` type helper in `models.ts`.

- [ ] **Step 1: Regenerate and inspect the API types**

```bash
make api-types
grep -n "kind: 'paragraph'" apps/web/src/app/generated/types.gen.ts
grep -n "kind: 'inlineCheck'" apps/web/src/app/generated/types.gen.ts
```

Expected: both greps print at least one line. The unions `RichBlock`, `RichInline`, `MathNode` and `LessonBlock` must be discriminated by a literal `kind`. If `kind` is generated as plain `string` or as optional, stop and fix the OpenAPI output first; every component below relies on the discriminator for narrowing.

- [ ] **Step 2: Export the new types**

Replace `apps/web/src/app/models.ts`:

```ts
// All API types are generated from the OpenAPI document (`make api-types`); never edit generated/.
import type { LessonBlock } from './generated/types.gen';

export type {
  Answer,
  Attribution,
  AttemptSummaryDto as AttemptSummary,
  AttemptView as Attempt,
  Blueprint,
  CatalogMetadata,
  CatalogSummaryDto as CourseCard,
  CourseCatalogDto,
  CourseGoal,
  CourseGoalStatusDto,
  CourseLevel,
  CourseProgressDto,
  CurrentUserResponse as User,
  DashboardDto,
  DeliveryQuestion as Question,
  Diagnostic,
  EnrollmentStatus,
  FeedbackDto,
  GlossaryEntry,
  Grade,
  Grading,
  InlineCheckResultDto,
  Lesson,
  LessonBlock,
  MasteryState,
  MathNode,
  Module,
  NextStepDto,
  NextStepKind,
  NextStepReason,
  Objective,
  ObjectiveMasteryDto,
  Option,
  PreviewDto,
  RichBlock,
  RichInline,
  TextDirection,
  ValidationResponseDto,
} from './generated/types.gen';

// One member of the lesson-block union, e.g. BlockOf<'table'>.
export type BlockOf<K extends LessonBlock['kind']> = Extract<LessonBlock, { kind: K }>;
```

- [ ] **Step 3: Write the failing renderer tests**

`apps/web/src/app/rich/rich.spec.ts`:

```ts
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { MathNode, RichBlock, RichInline } from '../models';
import { Blocks } from './blocks';
import { RichContext } from './context';
import { Inlines } from './inlines';
import { MathView } from './math';
import { plainBlocks, plainInlines, plainMath } from './plain-text';

const MATHML = 'http://www.w3.org/1998/Math/MathML';
const text = (value: string) => ({ kind: 'text', text: value }) as RichInline;

async function render<T>(component: Type<T>, inputs: Record<string, unknown>, setup?: (context: RichContext) => void) {
  TestBed.configureTestingModule({ providers: [provideRouter([]), RichContext] });
  setup?.(TestBed.inject(RichContext));
  const fixture = TestBed.createComponent(component);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  await fixture.whenStable();
  return { element: fixture.nativeElement as HTMLElement, fixture };
}

describe('rich inlines', () => {
  it('renders content as text, never as HTML', async () => {
    const { element } = await render(Inlines, { nodes: [text('<b>bold</b> & more')] });
    expect(element.querySelector('b')).toBeNull();
    expect(element.textContent).toBe('<b>bold</b> & more');
  });

  it('maps formatting, code, breaks and links to elements', async () => {
    const { element } = await render(Inlines, {
      nodes: [
        { kind: 'emphasis', inlines: [text('em')] },
        { kind: 'strong', inlines: [text('st')] },
        { kind: 'code', code: 'x' },
        { kind: 'break' },
        { kind: 'link', href: 'https://example.org/', inlines: [text('site')] },
      ] as RichInline[],
    });
    expect(element.querySelector('em')?.textContent).toBe('em');
    expect(element.querySelector('strong')?.textContent).toBe('st');
    expect(element.querySelector('code')?.textContent).toBe('x');
    expect(element.querySelector('br')).not.toBeNull();
    const link = element.querySelector('a')!;
    expect(link.getAttribute('href')).toBe('https://example.org/');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('links lessons through the router and marks language spans', async () => {
    const { element } = await render(
      Inlines,
      {
        nodes: [
          { kind: 'lessonLink', lessonId: 'sets', blockId: 'b1', inlines: [text('sets')] },
          { kind: 'lang', language: 'ar', direction: 'rtl', inlines: [text('نص')] },
        ] as RichInline[],
      },
      (context) => context.packId.set('demo'),
    );
    expect(element.querySelector('a')!.getAttribute('href')).toBe('/courses/demo?tab=learn&lesson=sets&block=b1');
    expect(element.querySelector('[lang="ar"]')!.getAttribute('dir')).toBe('rtl');
  });

  it('turns glossary terms into disclosures and leaves terms without a glossary as text', async () => {
    const { element, fixture } = await render(
      Inlines,
      {
        nodes: [
          { kind: 'term', termId: 'union', inlines: [text('union')] },
          text(' and '),
          { kind: 'term', termId: 'missing', inlines: [text('other')] },
        ] as RichInline[],
      },
      (context) => context.glossary.set([{ id: 'union', term: 'union', definition: [text('either set')] }]),
    );
    const buttons = element.querySelectorAll('button');
    expect(buttons.length).toBe(1);
    const button = buttons[0];
    const definition = element.querySelector(`#${button.getAttribute('aria-controls')}`) as HTMLElement;
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(definition.hidden).toBe(true);
    button.click();
    await fixture.whenStable();
    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(definition.hidden).toBe(false);
    expect(definition.textContent).toContain('either set');
    expect(element.textContent).toContain('other');
  });

  it('renders links as text when the context is not interactive', async () => {
    const { element } = await render(
      Inlines,
      { nodes: [{ kind: 'link', href: 'https://example.org/', inlines: [text('site')] }] as RichInline[] },
      (context) => context.interactive.set(false),
    );
    expect(element.querySelector('a')).toBeNull();
    expect(element.textContent).toBe('site');
  });
});

describe('rich blocks', () => {
  it('renders paragraphs, numbered lists, quotes and labelled code regions', async () => {
    const { element } = await render(Blocks, {
      blocks: [
        { kind: 'paragraph', inlines: [text('p')] },
        { kind: 'list', ordered: true, start: 3, items: [[{ kind: 'paragraph', inlines: [text('three')] }]] },
        { kind: 'list', ordered: false, start: 1, items: [[{ kind: 'paragraph', inlines: [text('dot')] }]] },
        { kind: 'quote', blocks: [{ kind: 'paragraph', inlines: [text('q')] }] },
        { kind: 'codeBlock', language: 'python', code: 'print(1)' },
      ] as RichBlock[],
    });
    expect(element.querySelector('ol')!.getAttribute('start')).toBe('3');
    expect(element.querySelector('ul li')!.textContent).toBe('dot');
    expect(element.querySelector('blockquote p')!.textContent).toBe('q');
    const region = element.querySelector('[role="region"]')!;
    expect(region.getAttribute('tabindex')).toBe('0');
    expect(element.querySelector(`#${region.getAttribute('aria-labelledby')}`)!.textContent).toBe('Code: python');
    expect(region.querySelector('pre code')!.textContent).toBe('print(1)');
  });
});

describe('math', () => {
  it('renders native MathML elements', async () => {
    const root = {
      kind: 'mrow',
      children: [
        { kind: 'mfrac', numerator: { kind: 'mn', text: '1' }, denominator: { kind: 'mi', text: 'x', normal: false } },
        { kind: 'mi', text: 'sin', normal: true },
        { kind: 'mtable', rows: [[{ kind: 'mn', text: '1' }, { kind: 'mn', text: '0' }]], columnAlign: null },
      ],
    } as MathNode;
    const { element } = await render(MathView, { root, display: true, describedBy: 'desc' });
    const math = element.querySelector('math')!;
    expect(math.namespaceURI).toBe(MATHML);
    expect(math.getAttribute('display')).toBe('block');
    expect(math.getAttribute('aria-describedby')).toBe('desc');
    const fraction = element.querySelector('mfrac')!;
    expect(fraction.namespaceURI).toBe(MATHML);
    expect(fraction.children.length).toBe(2);
    expect(element.querySelector('mi[mathvariant="normal"]')!.textContent).toBe('sin');
    expect(element.querySelectorAll('mtr mtd').length).toBe(2);
  });
});

describe('plain text', () => {
  it('flattens nodes and reads math as Unicode text', () => {
    const and = { kind: 'mrow', children: [{ kind: 'mi', text: 'P' }, { kind: 'mo', text: '∧' }, { kind: 'mi', text: 'Q' }] } as MathNode;
    expect(plainInlines([text('Is '), { kind: 'math', tex: 'P \\land Q', root: and } as RichInline, text('?')])).toBe('Is P ∧ Q?');
    const set = { kind: 'mrow', children: [{ kind: 'mo', text: '{' }, { kind: 'mn', text: '1' }, { kind: 'mo', text: ',' }, { kind: 'mn', text: '2' }, { kind: 'mo', text: '}' }] } as MathNode;
    expect(plainMath(set).trim()).toBe('{1, 2}');
    expect(plainBlocks([{ kind: 'paragraph', inlines: [text('one')] }, { kind: 'codeBlock', language: 'text', code: 'two' }] as RichBlock[])).toBe('one two');
  });
});
```

`apps/web/src/app/rich/lesson-block.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { LessonBlock, RichInline } from '../models';
import { RichContext } from './context';
import { LessonBlockView } from './lesson-block';

const text = (value: string) => [{ kind: 'text', text: value }] as RichInline[];
const paragraph = (value: string) => [{ kind: 'paragraph', inlines: text(value) }];

async function renderBlock(block: object, parentLanguage: string | null = 'en') {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), RichContext] });
  const fixture = TestBed.createComponent(LessonBlockView);
  fixture.componentRef.setInput('block', block as LessonBlock);
  fixture.componentRef.setInput('lessonId', 'intro');
  fixture.componentRef.setInput('parentLanguage', parentLanguage);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('lesson blocks', () => {
  it('anchors each block and marks a language that differs from its parent', async () => {
    const element = await renderBlock({ kind: 'text', id: 'b1', title: 'Title', language: 'fr', direction: 'ltr', body: paragraph('Bonjour') });
    const section = element.querySelector('section')!;
    expect(section.id).toBe('block-intro-b1');
    expect(section.getAttribute('tabindex')).toBe('-1');
    expect(section.getAttribute('lang')).toBe('fr');
    expect(element.querySelector('h3')!.textContent).toBe('Title');
  });

  it('omits the language when it matches the parent', async () => {
    const element = await renderBlock({ kind: 'text', id: 'b2', title: null, language: 'en', direction: 'ltr', body: paragraph('Hi') });
    expect(element.querySelector('section')!.hasAttribute('lang')).toBe(false);
  });

  it('renders tables with a caption, column headers and row headers in a labelled region', async () => {
    const element = await renderBlock({
      kind: 'table', id: 't', title: null, language: null, direction: null, caption: 'Sizes', rowHeaders: true,
      columns: [text('Set'), text('Size')], rows: [[text('A'), text('2')]],
    });
    const region = element.querySelector('[role="region"]')!;
    const caption = element.querySelector('caption')!;
    expect(region.getAttribute('aria-labelledby')).toBe(caption.id);
    expect(caption.textContent).toBe('Sizes');
    expect([...element.querySelectorAll('th[scope="col"]')].map((th) => th.textContent)).toEqual(['Set', 'Size']);
    expect(element.querySelector('tbody th[scope="row"]')!.textContent).toBe('A');
    expect(element.querySelector('tbody td')!.textContent).toBe('2');
  });

  it('describes display math with its visible description', async () => {
    const element = await renderBlock({
      kind: 'math', id: 'm', title: null, language: null, direction: null, tex: 'x', root: { kind: 'mi', text: 'x', normal: false }, description: 'The letter x.',
    });
    const math = element.querySelector('math')!;
    expect(math.getAttribute('display')).toBe('block');
    expect(element.querySelector(`#${math.getAttribute('aria-describedby')}`)!.textContent).toBe('The letter x.');
  });

  it('labels the parts of a worked example', async () => {
    const worked = await renderBlock({
      kind: 'workedExample', id: 'w', title: null, language: null, direction: null,
      problem: paragraph('P'), steps: [paragraph('one'), paragraph('two')], result: paragraph('R'),
    });
    expect([...worked.querySelectorAll('.block-label')].map((p) => p.textContent)).toEqual(['Problem', 'Steps', 'Result']);
    expect(worked.querySelectorAll('ol.worked-steps > li').length).toBe(2);
  });

  it('labels a misconception and its correction', async () => {
    const element = await renderBlock({ kind: 'misconception', id: 'x', title: null, language: null, direction: null, claim: paragraph('C'), correction: paragraph('K') });
    expect([...element.querySelectorAll('.block-label')].map((p) => p.textContent)).toEqual(['Common misconception', 'Correction']);
  });

  it('titles a definition with its term', async () => {
    const element = await renderBlock({ kind: 'definition', id: 'd', language: null, direction: null, termId: 'u', term: 'union', definition: text('either set'), body: null });
    expect(element.querySelector('h3 dfn')!.textContent).toBe('union');
  });

  it('marks a primary source excerpt with its language and attributes it', async () => {
    const element = await renderBlock({
      kind: 'primarySource', id: 's', title: null, language: 'la', direction: 'ltr', excerpt: paragraph('Omnia mutantur.'),
      attribution: { title: 'Metamorphoses', author: 'Ovid', date: null, url: null }, translation: paragraph('Everything changes.'),
    });
    expect(element.querySelector('section')!.hasAttribute('lang')).toBe(false);
    expect(element.querySelector('blockquote')!.getAttribute('lang')).toBe('la');
    expect(element.querySelector('figcaption')!.textContent).toBe('— Ovid, Metamorphoses');
    expect(element.querySelector('.block-label')!.textContent).toBe('Translation');
  });
});
```

`apps/web/src/app/rich/inline-check.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { BlockOf, RichInline } from '../models';
import { RichContext } from './context';
import { InlineCheck } from './inline-check';

const text = (value: string) => [{ kind: 'text', text: value }] as RichInline[];
const block = {
  kind: 'inlineCheck', id: 'check', title: null, language: null, direction: null,
  question: {
    id: 'check', kind: 'single', prompt: [{ kind: 'paragraph', inlines: text('Pick A') }], objectiveIds: ['o'],
    options: [{ id: 'a', text: text('A') }, { id: 'b', text: text('B') }], slots: [], selectCount: 1, reuse: false, scenarioId: null, weight: 1,
  },
} as BlockOf<'inlineCheck'>;

async function setup(interactive = true) {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), RichContext] });
  const context = TestBed.inject(RichContext);
  context.packId.set('demo');
  context.version.set('2.0.0');
  context.interactive.set(interactive);
  const fixture = TestBed.createComponent(InlineCheck);
  fixture.componentRef.setInput('block', block);
  fixture.componentRef.setInput('lessonId', 'intro');
  await fixture.whenStable();
  return { fixture, element: fixture.nativeElement as HTMLElement, http: TestBed.inject(HttpTestingController) };
}

describe('inline check', () => {
  it('grades on the server, announces once and keeps focus on the button', async () => {
    const { fixture, element, http } = await setup();
    (element.querySelector('input[type=radio]') as HTMLInputElement).click();
    await fixture.whenStable();
    const button = [...element.querySelectorAll('button')].find((b) => b.textContent?.includes('Check answer'))!;
    button.focus();
    button.click();
    const request = http.expectOne('/api/catalog/demo/checks');
    expect(request.request.body).toEqual({ version: '2.0.0', lessonId: 'intro', blockId: 'check', answer: { selected: ['a'], slots: {} } });
    request.flush({
      grade: { earned: 1, possible: 1, fullyCorrect: true, correct: ['a'], matches: null },
      explanation: [{ kind: 'paragraph', inlines: text('Because.') }],
    });
    await fixture.whenStable();
    expect(element.querySelector('.check-status')!.textContent).toBe('Correct.');
    expect(element.querySelector('.feedback')!.textContent).toContain('Because.');
    expect(document.activeElement).toBe(button);
    http.verify();
  });

  it('cannot be answered in a preview', async () => {
    const { element } = await setup(false);
    expect([...element.querySelectorAll('button')].some((b) => b.textContent?.includes('Check answer'))).toBe(false);
    expect(element.textContent).toContain('after the pack is published');
    expect((element.querySelector('fieldset') as HTMLFieldSetElement).disabled).toBe(true);
  });
});
```

`apps/web/src/app/question-input.spec.ts`:

```ts
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { Question, RichInline } from './models';
import { QuestionInput } from './question-input';

const text = (value: string) => [{ kind: 'text', text: value }] as RichInline[];
const matching = {
  id: 'm1', kind: 'matching', prompt: [], objectiveIds: ['o'], selectCount: 1, reuse: false, scenarioId: null, weight: 1,
  options: [
    { id: 'a', text: text('Evaporation') },
    { id: 'b', text: [{ kind: 'math', tex: 'x', root: { kind: 'mi', text: 'x', normal: false } }] as RichInline[] },
  ],
  slots: [{ id: 's1', text: text('Cause'), options: [] }, { id: 's2', text: text('Effect'), options: [] }],
} as Question;

@Component({
  imports: [QuestionInput],
  template: `<lf-question-input [question]="question" instanceId="first" /><lf-question-input [question]="question" instanceId="second" />`,
})
class TwoInstances {
  readonly question = matching;
}

describe('question input', () => {
  it('keeps every generated id unique across instances', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(TwoInstances);
    await fixture.whenStable();
    const ids = [...(fixture.nativeElement as HTMLElement).querySelectorAll('[id]')].map((e) => e.id);
    expect(ids.length).toBeGreaterThan(0);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('renders rich token labels and plain dropdown options', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(QuestionInput);
    fixture.componentRef.setInput('instanceId', 'q');
    fixture.componentRef.setInput('question', {
      ...matching, kind: 'dropdown', options: [],
      slots: [{ id: 's1', text: text('Blank'), options: [{ id: 't', text: text('True') }, { id: 'f', text: text('False') }] }],
    } as Question);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect([...element.querySelectorAll('option')].map((o) => o.textContent)).toEqual(['Choose an answer…', 'True', 'False']);
    fixture.componentRef.setInput('question', matching);
    await fixture.whenStable();
    expect(element.querySelector('.token-bank math')).not.toBeNull();
  });
});
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `npm test --prefix apps/web`
Expected: FAIL. The new spec files cannot resolve `./blocks`, `./inlines` and the other renderers. The existing specs still pass.

- [ ] **Step 5: Create the shared helpers**

`rich/context.ts`:

```ts
import { Injectable, signal } from '@angular/core';
import { GlossaryEntry } from '../models';

// Page-level settings shared by the rich-content renderers. Provide it on the page component.
@Injectable()
export class RichContext {
  readonly packId = signal('');
  readonly version = signal('');
  readonly glossary = signal<readonly GlossaryEntry[]>([]);
  // False in the Studio preview: links render as text and inline checks cannot be answered.
  readonly interactive = signal(true);
}
```

`rich/ids.ts`:

```ts
let next = 0;

// Unique DOM IDs for label, description and disclosure relationships.
export const uniqueId = (prefix: string) => `${prefix}-${++next}`;
```

`rich/plain-text.ts`:

```ts
import { MathNode, RichBlock, RichInline } from '../models';

// Operators that read better without surrounding spaces.
const tight = new Set(['(', ')', '[', ']', '{', '}', '|', '‖', '⟨', '⟩', '.', '!', '′']);

// Plain text for places that cannot hold markup: native <option> elements, aria-label, summaries and review lines.
// Math reads as Unicode text (P ∧ Q), not TeX, because learners see and hear it. Core's PlainText keeps TeX for search.
export function plainMath(node: MathNode): string {
  switch (node.kind) {
    case 'mi':
    case 'mn':
    case 'mtext':
      return node.text;
    case 'mo':
      if (node.text === '\u2061') return ' ';
      if (node.text === ',') return ', ';
      return tight.has(node.text) ? node.text : ` ${node.text} `;
    case 'mspace':
      return ' ';
    case 'mrow':
      return node.children.map((child) => plainMath(child)).join('');
    case 'mfrac':
      return `${plainMath(node.numerator)}/${plainMath(node.denominator)}`;
    case 'msqrt':
      return `√(${plainMath(node.child)})`;
    case 'mroot':
      return `${plainMath(node.index)}√(${plainMath(node.base)})`;
    case 'msub':
      return `${plainMath(node.base)}_${plainMath(node.subscript)}`;
    case 'msup':
      return `${plainMath(node.base)}^${plainMath(node.superscript)}`;
    case 'msubsup':
      return `${plainMath(node.base)}_${plainMath(node.subscript)}^${plainMath(node.superscript)}`;
    case 'munder':
      return `${plainMath(node.base)}_${plainMath(node.under)}`;
    case 'mover':
      return `${plainMath(node.base)}${plainMath(node.over)}`;
    case 'munderover':
      return `${plainMath(node.base)}_${plainMath(node.under)}^${plainMath(node.over)}`;
    case 'mtable':
      return node.rows.map((row) => row.map((cell) => plainMath(cell)).join(' ')).join('; ');
    default:
      return '';
  }
}

export function plainInlines(nodes: readonly RichInline[]): string {
  return nodes
    .map((node) => {
      switch (node.kind) {
        case 'text':
          return node.text;
        case 'code':
          return node.code;
        case 'break':
          return ' ';
        case 'math':
          return plainMath(node.root).replace(/\s+/g, ' ').trim();
        case 'emphasis':
        case 'strong':
        case 'link':
        case 'lessonLink':
        case 'term':
        case 'lang':
          return plainInlines(node.inlines);
        default:
          return '';
      }
    })
    .join('');
}

export function plainBlocks(blocks: readonly RichBlock[]): string {
  return blocks
    .map((block) => {
      switch (block.kind) {
        case 'paragraph':
          return plainInlines(block.inlines);
        case 'list':
          return block.items.map((item) => plainBlocks(item)).join(' ');
        case 'quote':
          return plainBlocks(block.blocks);
        case 'codeBlock':
          return block.code;
        default:
          return '';
      }
    })
    .join(' ');
}
```

- [ ] **Step 6: Create the math, code, inline and block renderers**

Keep these templates exactly as written. Whitespace inside `@case` bodies is rendered text.

`rich/math.ts`:

```ts
import { NgTemplateOutlet } from '@angular/common';
import { Component, input } from '@angular/core';
import { MathNode } from '../models';

// Renders MathML-shaped nodes as native MathML. The recursive template sits outside <math>, where Angular would
// otherwise create HTML elements, so every element carries the explicit math: namespace prefix.
@Component({
  selector: 'lf-math',
  imports: [NgTemplateOutlet],
  template: `<math [attr.display]="display() ? 'block' : null" [attr.aria-describedby]="describedBy()"><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: root() }" /></math>
    <ng-template #node let-n>
      @switch (n.kind) {
        @case ('mi') {<math:mi [attr.mathvariant]="n.normal ? 'normal' : null">{{ n.text }}</math:mi>}
        @case ('mn') {<math:mn>{{ n.text }}</math:mn>}
        @case ('mo') {<math:mo [attr.stretchy]="n.stretchy ? 'true' : null" [attr.largeop]="n.largeOp ? 'true' : null">{{ n.text }}</math:mo>}
        @case ('mtext') {<math:mtext>{{ n.text }}</math:mtext>}
        @case ('mspace') {<math:mspace [attr.width]="n.width" />}
        @case ('mrow') {<math:mrow>@for (child of n.children; track $index) {<ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: child }" />}</math:mrow>}
        @case ('mfrac') {<math:mfrac><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.numerator }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.denominator }" /></math:mfrac>}
        @case ('msqrt') {<math:msqrt><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.child }" /></math:msqrt>}
        @case ('mroot') {<math:mroot><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.base }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.index }" /></math:mroot>}
        @case ('msub') {<math:msub><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.base }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.subscript }" /></math:msub>}
        @case ('msup') {<math:msup><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.base }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.superscript }" /></math:msup>}
        @case ('msubsup') {<math:msubsup><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.base }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.subscript }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.superscript }" /></math:msubsup>}
        @case ('munder') {<math:munder><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.base }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.under }" /></math:munder>}
        @case ('mover') {<math:mover [attr.accent]="n.accent ? 'true' : null"><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.base }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.over }" /></math:mover>}
        @case ('munderover') {<math:munderover><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.base }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.under }" /><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: n.over }" /></math:munderover>}
        @case ('mtable') {<math:mtable [attr.columnalign]="n.columnAlign">@for (row of n.rows; track $index) {<math:mtr>@for (cell of row; track $index) {<math:mtd><ng-container [ngTemplateOutlet]="node" [ngTemplateOutletContext]="{ $implicit: cell }" /></math:mtd>}</math:mtr>}</math:mtable>}
      }
    </ng-template>`,
})
export class MathView {
  readonly root = input.required<MathNode>();
  readonly display = input(false);
  readonly describedBy = input<string | null>(null);
}
```

`rich/code.ts`:

```ts
import { Component, input } from '@angular/core';
import { uniqueId } from './ids';

// A focusable, labelled scroll region: long lines scroll on their own at narrow widths without trapping focus.
@Component({
  selector: 'lf-code',
  template: `<div class="code-region" role="region" tabindex="0" [attr.aria-labelledby]="labelId">
    <span class="code-language" [id]="labelId">Code: {{ language() || 'plain text' }}</span>
    <pre><code>{{ code() }}</code></pre>
  </div>`,
})
export class CodeRegion {
  readonly language = input<string | null | undefined>(null);
  readonly code = input.required<string>();
  readonly labelId = uniqueId('code');
}
```

`rich/inlines.ts`:

```ts
import { Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GlossaryEntry, RichInline } from '../models';
import { RichContext } from './context';
import { uniqueId } from './ids';
import { MathView } from './math';

// Renders inline rich text. Every node becomes an element or a text binding; nothing is parsed or injected as HTML.
// A glossary term is a disclosure button that reveals its definition inline; without a glossary it is plain text.
@Component({
  selector: 'lf-inlines',
  imports: [RouterLink, MathView],
  template: `@for (node of nodes(); track $index; let i = $index) {
    @switch (node.kind) {
      @case ('text') {{{ node.text }}}
      @case ('emphasis') {<em><lf-inlines [nodes]="node.inlines" /></em>}
      @case ('strong') {<strong><lf-inlines [nodes]="node.inlines" /></strong>}
      @case ('code') {<code>{{ node.code }}</code>}
      @case ('break') {<br />}
      @case ('link') {
        @if (interactive()) {<a class="text-link" [href]="node.href" rel="noopener noreferrer"><lf-inlines [nodes]="node.inlines" /></a>} @else {<lf-inlines [nodes]="node.inlines" />}
      }
      @case ('lessonLink') {
        @if (interactive() && packId()) {<a class="text-link" [routerLink]="['/courses', packId()]" [queryParams]="{ tab: 'learn', lesson: node.lessonId, block: node.blockId }"><lf-inlines [nodes]="node.inlines" /></a>} @else {<lf-inlines [nodes]="node.inlines" />}
      }
      @case ('term') {
        @if (entry(node.termId); as e) {<button type="button" class="term" [attr.aria-expanded]="open().has(i)" [attr.aria-controls]="termId(i)" (click)="toggle(i)"><lf-inlines [nodes]="node.inlines" /></button><span class="term-definition" [id]="termId(i)" [hidden]="!open().has(i)"><dfn>{{ e.term }}</dfn>: <lf-inlines [nodes]="e.definition" /></span>} @else {<lf-inlines [nodes]="node.inlines" />}
      }
      @case ('lang') {<span [attr.lang]="node.language" [attr.dir]="node.direction"><lf-inlines [nodes]="node.inlines" /></span>}
      @case ('math') {<lf-math [root]="node.root" />}
    }
  }`,
})
export class Inlines {
  private readonly context = inject(RichContext, { optional: true });
  private readonly ids = new Map<number, string>();
  readonly nodes = input.required<readonly RichInline[]>();
  readonly open = signal<ReadonlySet<number>>(new Set());
  interactive() {
    return this.context?.interactive() ?? true;
  }
  packId() {
    return this.context?.packId() ?? '';
  }
  entry(termId: string): GlossaryEntry | undefined {
    return this.context?.glossary().find((g) => g.id === termId);
  }
  termId(index: number) {
    let id = this.ids.get(index);
    if (!id) this.ids.set(index, (id = uniqueId('term')));
    return id;
  }
  toggle(index: number) {
    const next = new Set(this.open());
    if (!next.delete(index)) next.add(index);
    this.open.set(next);
  }
}
```

If the Angular template lexer rejects `@case ('text') {{{ node.text }}}`, write `@case ('text') {<ng-container>{{ node.text }}</ng-container>}` instead. Never add spaces around the interpolation.

`rich/blocks.ts`:

```ts
import { Component, input } from '@angular/core';
import { RichBlock } from '../models';
import { CodeRegion } from './code';
import { Inlines } from './inlines';

@Component({
  selector: 'lf-blocks',
  imports: [Inlines, CodeRegion],
  template: `@for (block of blocks(); track $index) {
    @switch (block.kind) {
      @case ('paragraph') {<p><lf-inlines [nodes]="block.inlines" /></p>}
      @case ('list') {
        @if (block.ordered) {
          <ol [attr.start]="block.start === 1 ? null : block.start">@for (item of block.items; track $index) {<li><lf-blocks [blocks]="item" /></li>}</ol>
        } @else {
          <ul>@for (item of block.items; track $index) {<li><lf-blocks [blocks]="item" /></li>}</ul>
        }
      }
      @case ('quote') {<blockquote><lf-blocks [blocks]="block.blocks" /></blockquote>}
      @case ('codeBlock') {<lf-code [language]="block.language" [code]="block.code" />}
    }
  }`,
})
export class Blocks {
  readonly blocks = input.required<readonly RichBlock[]>();
}
```

- [ ] **Step 7: Create the lesson-block and inline-check components**

`rich/inline-check.ts`:

```ts
import { Component, computed, inject, input, signal } from '@angular/core';
import { Api, message } from '../api';
import { Answer, BlockOf, InlineCheckResultDto } from '../models';
import { QuestionInput } from '../question-input';
import { Blocks } from './blocks';
import { RichContext } from './context';

// A formative question inside a lesson. The server grades it; nothing is recorded and the learner may check again.
// Focus stays on the button and the result is announced once.
@Component({
  selector: 'lf-inline-check',
  imports: [Blocks, QuestionInput],
  template: `<div class="check-prompt" [id]="promptId()"><lf-blocks [blocks]="block().question.prompt" /></div>
    <lf-question-input
      [question]="block().question"
      [instanceId]="instanceId()"
      [promptId]="promptId()"
      [answer]="answer()"
      [disabled]="busy() || !interactive()"
      (changed)="change($event)"
    />
    @if (interactive()) {
      <div class="row">
        <button type="button" class="button" [attr.aria-disabled]="busy() ? 'true' : null" (click)="check()">Check answer</button>
      </div>
    } @else {
      <p class="muted small">Inline checks can be answered after the pack is published.</p>
    }
    <p class="sr-only check-status" aria-live="polite">{{ announcement() }}</p>
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    }
    @if (result(); as r) {
      <div class="feedback" [class.correct]="r.grade.fullyCorrect">
        <strong>{{ verdict(r) }}</strong>
        <lf-blocks [blocks]="r.explanation" />
      </div>
    }`,
})
export class InlineCheck {
  private readonly api = inject(Api);
  private readonly context = inject(RichContext, { optional: true });
  readonly block = input.required<BlockOf<'inlineCheck'>>();
  readonly lessonId = input.required<string>();
  readonly instanceId = computed(() => `check-${this.lessonId()}-${this.block().id}`);
  readonly promptId = computed(() => `${this.instanceId()}-prompt`);
  readonly answer = signal<Answer>({ selected: [], slots: {} });
  readonly result = signal<InlineCheckResultDto | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly announcement = signal('');
  interactive() {
    return this.context?.interactive() ?? true;
  }
  change(answer: Answer) {
    this.answer.set(answer);
    this.result.set(null);
  }
  verdict(r: InlineCheckResultDto) {
    if (r.grade.fullyCorrect) return 'Correct.';
    return r.grade.earned > 0 ? `Partly correct: ${r.grade.earned} of ${r.grade.possible} points.` : 'Not quite. Read the explanation below.';
  }
  async check() {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.announcement.set('');
    try {
      const result = await this.api.post<InlineCheckResultDto>(`/catalog/${this.context?.packId()}/checks`, {
        version: this.context?.version(),
        lessonId: this.lessonId(),
        blockId: this.block().id,
        answer: this.answer(),
      });
      this.result.set(result);
      this.announcement.set(this.verdict(result));
    } catch (e) {
      this.error.set(`${message(e)} Try again.`);
    } finally {
      this.busy.set(false);
    }
  }
}
```

`rich/lesson-block.ts`:

```ts
import { Component, computed, input } from '@angular/core';
import { Attribution, LessonBlock } from '../models';
import { Blocks } from './blocks';
import { CodeRegion } from './code';
import { uniqueId } from './ids';
import { InlineCheck } from './inline-check';
import { Inlines } from './inlines';
import { MathView } from './math';

// One lesson block: an anchor for links and focus, its language, and an accessible structure per kind.
@Component({
  selector: 'lf-lesson-block',
  imports: [Blocks, Inlines, MathView, CodeRegion, InlineCheck],
  template: `@let b = block();
    <section [class]="'content-block ' + b.kind" [id]="'block-' + lessonId() + '-' + b.id" tabindex="-1" [attr.lang]="language()" [attr.dir]="direction()">
      @switch (b.kind) {
        @case ('text') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <lf-blocks [blocks]="b.body" />
        }
        @case ('callout') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <lf-blocks [blocks]="b.body" />
        }
        @case ('example') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <lf-blocks [blocks]="b.body" />
        }
        @case ('code') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <lf-code [language]="b.language" [code]="b.code" />
        }
        @case ('math') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <figure class="math-figure">
            <lf-math [root]="b.root" [display]="true" [describedBy]="descriptionId" />
            <figcaption [id]="descriptionId">{{ b.description }}</figcaption>
          </figure>
        }
        @case ('table') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <div class="table-region" role="region" tabindex="0" [attr.aria-labelledby]="captionId">
            <table>
              <caption [id]="captionId">{{ b.caption }}</caption>
              <thead>
                <tr>@for (column of b.columns; track $index) {<th scope="col"><lf-inlines [nodes]="column" /></th>}</tr>
              </thead>
              <tbody>
                @for (row of b.rows; track $index) {
                  <tr>@for (cell of row; track $index; let first = $first) {@if (first && b.rowHeaders) {<th scope="row"><lf-inlines [nodes]="cell" /></th>} @else {<td><lf-inlines [nodes]="cell" /></td>}}</tr>
                }
              </tbody>
            </table>
          </div>
        }
        @case ('workedExample') {
          <h3>{{ b.title || 'Worked example' }}</h3>
          <p class="block-label">Problem</p>
          <lf-blocks [blocks]="b.problem" />
          <p class="block-label">Steps</p>
          <ol class="worked-steps">@for (step of b.steps; track $index) {<li><lf-blocks [blocks]="step" /></li>}</ol>
          <p class="block-label">Result</p>
          <lf-blocks [blocks]="b.result" />
        }
        @case ('misconception') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <p class="block-label">Common misconception</p>
          <lf-blocks [blocks]="b.claim" />
          <p class="block-label">Correction</p>
          <lf-blocks [blocks]="b.correction" />
        }
        @case ('definition') {
          <h3><dfn>{{ b.term }}</dfn></h3>
          <p><lf-inlines [nodes]="b.definition" /></p>
          @if (b.body) {<lf-blocks [blocks]="b.body" />}
        }
        @case ('primarySource') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <figure class="primary-source">
            <blockquote [attr.lang]="b.language" [attr.dir]="b.language ? b.direction : null"><lf-blocks [blocks]="b.excerpt" /></blockquote>
            <figcaption>{{ byline(b.attribution) }}@if (b.attribution.url) {<a class="text-link" [href]="b.attribution.url" rel="noopener noreferrer"><cite>{{ b.attribution.title }}</cite></a>} @else {<cite>{{ b.attribution.title }}</cite>}{{ dateline(b.attribution) }}</figcaption>
          </figure>
          @if (b.translation) {
            <p class="block-label">Translation</p>
            <lf-blocks [blocks]="b.translation" />
          }
        }
        @case ('inlineCheck') {
          @if (b.title) {<h3>{{ b.title }}</h3>}
          <lf-inline-check [block]="b" [lessonId]="lessonId()" />
        }
      }
    </section>`,
})
export class LessonBlockView {
  readonly block = input.required<LessonBlock>();
  readonly lessonId = input.required<string>();
  readonly parentLanguage = input<string | null | undefined>(null);
  readonly descriptionId = uniqueId('math-description');
  readonly captionId = uniqueId('table-caption');
  // Code carries a programming language, and primary sources mark only the excerpt; other blocks mark the section.
  readonly language = computed(() => {
    const b = this.block();
    if (b.kind === 'code' || b.kind === 'primarySource' || !b.language || b.language === this.parentLanguage()) return null;
    return b.language;
  });
  readonly direction = computed(() => {
    const b = this.block();
    return this.language() && b.kind !== 'code' ? (b.direction ?? null) : null;
  });
  byline(a: Attribution) {
    return a.author ? `— ${a.author}, ` : '— ';
  }
  dateline(a: Attribution) {
    return a.date ? `, ${a.date}` : '';
  }
}
```

- [ ] **Step 8: Update the question input**

Replace `apps/web/src/app/question-input.ts`:

```ts
import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CdkDrag, CdkDropList, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { Answer, Option, Question, RichInline } from './models';
import { Inlines } from './rich/inlines';
import { plainInlines } from './rich/plain-text';

// instanceId keeps every generated ID unique when several questions render on one page (inline checks, previews).
@Component({
  selector: 'lf-question-input',
  imports: [FormsModule, CdkDrag, CdkDropList, Inlines],
  template: ` @if (question().kind === 'single' || question().kind === 'multiple') {
      <fieldset [disabled]="disabled()" [attr.aria-describedby]="promptId()">
        <legend>
          {{
            question().kind === 'single'
              ? 'Select one answer.'
              : 'Select ' + question().selectCount + ' answers.'
          }}
        </legend>
        <div class="choices">
          @for (option of question().options; track option.id; let i = $index) {
            <label class="choice" [class.selected]="answer().selected.includes(option.id)"
              ><input
                [type]="question().kind === 'single' ? 'radio' : 'checkbox'"
                [name]="instanceId()"
                [checked]="answer().selected.includes(option.id)"
                [disabled]="question().kind === 'multiple' && answer().selected.length >= question().selectCount && !answer().selected.includes(option.id)"
                (change)="choose(option.id)"
              /><span class="option-letter">{{ letter(i) }}</span
              ><span><lf-inlines [nodes]="option.text" /></span></label
            >
          }
        </div>
      </fieldset>
    } @else if (question().kind === 'sequence') {
      <p class="muted">Arrange the sequence. Drag items or use the move buttons.</p>
      <div cdkDropList [cdkDropListDisabled]="disabled()" (cdkDropListDropped)="drop($event)" class="sequence-list">
        @for (option of sequence(); track option.id; let i = $index) {
          <div class="sequence-item" cdkDrag [cdkDragDisabled]="disabled()">
            <span class="drag-handle" aria-hidden="true">⠿</span
            ><span class="muted">{{ i + 1 }}.</span><strong><lf-inlines [nodes]="option.text" /></strong>
            <div class="row">
              <button type="button" class="icon-button" [disabled]="disabled() || i === 0" [attr.aria-label]="'Move ' + plain(option.text) + ' up'" (click)="move(i, -1)">↑</button
              ><button
                type="button"
                class="icon-button"
                [disabled]="disabled() || i === sequence().length - 1"
                [attr.aria-label]="'Move ' + plain(option.text) + ' down'"
                (click)="move(i, 1)"
              >
                ↓
              </button>
            </div>
          </div>
        }
      </div>
      <button class="button secondary" [disabled]="disabled()" (click)="changed.emit({ selected: sequenceIds(), slots: {} })">Save this order</button>
    } @else if (question().kind === 'matching') {
      <p class="muted">
        Drag a token to its target, or select a token and then a target.
        {{ question().reuse ? 'Tokens may be reused.' : 'Use each token once.' }}
      </p>
      <div class="token-bank" cdkDropList [id]="instanceId() + '-bank'" [cdkDropListConnectedTo]="targetIds()" [cdkDropListSortingDisabled]="true">
        @for (option of question().options; track option.id) {
          <button
            type="button"
            cdkDrag
            [cdkDragData]="option.id"
            [cdkDragDisabled]="disabled()"
            [disabled]="disabled()"
            [attr.aria-pressed]="picked() === option.id"
            [class.selected]="picked() === option.id"
            class="token"
            (click)="picked.set(option.id)"
          ><lf-inlines [nodes]="option.text" /></button>
        }
      </div>
      <div class="matching-slots">
        @for (slot of question().slots; track slot.id) {
          <div class="match-target" cdkDropList [id]="targetId(slot.id)" [cdkDropListDisabled]="disabled()" (cdkDropListDropped)="place(slot.id, $event.item.data)">
            <span><lf-inlines [nodes]="slot.text" /></span
            ><button type="button" class="token target" [disabled]="disabled() || !picked()" (click)="place(slot.id, picked()!)">
              @if (optionFor(answer().slots[slot.id]); as placed) {<lf-inlines [nodes]="placed.text" />} @else {Place token here}</button
            ><button
              type="button"
              class="text-button"
              [disabled]="disabled() || !answer().slots[slot.id]"
              [attr.aria-label]="'Clear ' + plain(slot.text)"
              (click)="setSlot(slot.id, '')"
            >
              Clear
            </button>
          </div>
        }
      </div>
    } @else if (question().kind === 'dropdown') {
      <fieldset [disabled]="disabled()" [attr.aria-describedby]="promptId()">
        <legend>Choose an option for every blank.</legend>
        @for (slot of question().slots; track slot.id) {
          <label
            ><lf-inlines [nodes]="slot.text" /><select [ngModel]="answer().slots[slot.id] || ''" (ngModelChange)="setSlot(slot.id, $event)">
              <option value="">Choose an answer…</option>
              @for (option of slot.options; track option.id) {
                <option [value]="option.id">{{ plain(option.text) }}</option>
              }
            </select></label
          >
        }
      </fieldset>
    }
    <p class="sr-only" aria-live="polite">{{ announcement() }}</p>`,
})
export class QuestionInput {
  readonly question = input.required<Question>();
  readonly instanceId = input.required<string>();
  readonly promptId = input<string | null>(null);
  readonly answer = input<Answer>({ selected: [], slots: {} });
  readonly disabled = input(false);
  readonly changed = output<Answer>();
  readonly picked = signal<string | null>(null);
  readonly announcement = signal('');
  readonly sequence = computed(() => {
    const q = this.question();
    const ids = this.answer().selected;
    return ids.length === q.options.length ? ids.map((id) => q.options.find((o) => o.id === id)!).filter(Boolean) : q.options;
  });
  readonly targetIds = computed(() => this.question().slots.map((s) => this.targetId(s.id)));
  targetId(slotId: string) {
    return `${this.instanceId()}-target-${slotId}`;
  }
  letter(i: number) {
    return String.fromCharCode(65 + i);
  }
  plain(nodes: readonly RichInline[]) {
    return plainInlines(nodes);
  }
  sequenceIds() {
    return this.sequence().map((o) => o.id);
  }
  optionFor(id: string | undefined): Option | undefined {
    return this.question().options.find((o) => o.id === id);
  }
  optionText(id: string) {
    const option = this.optionFor(id);
    return option ? plainInlines(option.text) : '';
  }
  choose(id: string) {
    let selected = [...this.answer().selected];
    if (this.question().kind === 'single') selected = [id];
    else if (selected.includes(id)) selected = selected.filter((x) => x !== id);
    else if (selected.length < this.question().selectCount) selected.push(id);
    else return;
    this.changed.emit({ selected, slots: {} });
  }
  setSlot(id: string, value: string) {
    const slots = { ...this.answer().slots };
    if (value) slots[id] = value;
    else delete slots[id];
    this.changed.emit({ selected: [], slots });
  }
  place(id: string, value: string) {
    if (this.disabled() || !value) return;
    const slots = { ...this.answer().slots };
    if (!this.question().reuse) for (const key of Object.keys(slots)) if (slots[key] === value) delete slots[key];
    slots[id] = value;
    this.changed.emit({ selected: [], slots });
    this.picked.set(null);
    this.announcement.set('Placed ' + this.optionText(value));
  }
  move(index: number, delta: number) {
    const ids = this.sequenceIds();
    moveItemInArray(ids, index, index + delta);
    this.changed.emit({ selected: ids, slots: {} });
    this.announcement.set('Moved to position ' + (index + delta + 1));
  }
  drop(event: CdkDragDrop<unknown>) {
    const ids = this.sequenceIds();
    moveItemInArray(ids, event.previousIndex, event.currentIndex);
    this.changed.emit({ selected: ids, slots: {} });
  }
}
```

- [ ] **Step 9: Render the attempt player with the AST**

In `apps/web/src/app/pages/attempt.ts`:

1. Imports and component metadata:

```ts
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { Answer, Attempt, Grade, Option, Question } from '../models';
import { QuestionInput } from '../question-input';
import { Blocks } from '../rich/blocks';
import { RichContext } from '../rich/context';
import { plainBlocks, plainInlines } from '../rich/plain-text';
@Component({
  imports: [RouterLink, DecimalPipe, DatePipe, QuestionInput, Blocks],
  providers: [RichContext],
```

2. In the review loop, replace from `@if (a.results?.[q.id]; as g) {` to the closing `</details>` with:

```html
          @if (a.results?.[q.id]; as f) {
            <details class="panel review-item">
              <summary>
                <span class="review-number">{{ i + 1 }}</span
                ><span>{{ plainPrompt(q) }}</span
                ><span class="pill" [class.success]="f.grade.fullyCorrect"
                  >{{ f.grade.earned | number: '1.0-2' }} / {{ f.grade.possible }}</span
                >
              </summary>
              <div class="review-body">
                <div class="review-prompt"><lf-blocks [blocks]="q.prompt" /></div>
                <p><strong>Your answer:</strong> {{ describe(q, a.answers[q.id]) }}</p>
                <p><strong>Expected:</strong> {{ expected(q, f.grade) }}</p>
                <div class="explanation"><lf-blocks [blocks]="f.explanation" /></div>
                <a [routerLink]="['/courses', a.packId]" class="text-link">Revisit the lesson →</a>
              </div>
            </details>
          }
```

3. Scenario: replace `<p>{{ s.background }}</p>` with `<lf-blocks [blocks]="s.background" />`.

4. Question card: replace the `.row` heading, the `<h2>{{ q.prompt }}</h2>`, the `<lf-question-input …/>` and the feedback block with:

```html
                <div class="row">
                  <h2 class="eyebrow">QUESTION {{ index() + 1 }} / {{ a.questions.length }}</h2
                  ><span class="pill subtle">{{ kindLabel(q.kind) }}</span>
                </div>
                <div class="question-prompt" [id]="'prompt-' + q.id"><lf-blocks [blocks]="q.prompt" /></div>
                <lf-question-input
                  [question]="q"
                  [instanceId]="'q-' + q.id"
                  [promptId]="'prompt-' + q.id"
                  [answer]="a.answers[q.id] || empty"
                  [disabled]="busy() || !!a.feedback[q.id] || !!unsaved()"
                  (changed)="save($event)"
                />
                @if (a.feedback[q.id]; as f) {
                  <div class="feedback" [class.correct]="f.grade.fullyCorrect">
                    <strong
                      >{{ f.grade.fullyCorrect ? 'Correct' : 'Keep building' }} ·
                      {{ f.grade.earned | number: '1.0-2' }} / {{ f.grade.possible }}</strong
                    >
                    <lf-blocks [blocks]="f.explanation" />
                    <p class="small">Expected: {{ expected(q, f.grade) }}</p>
                  </div>
                }
```

5. In the class, inject the context and keep it in sync:

```ts
  private readonly rich = inject(RichContext);
```

and at the top of `accept(a: Attempt)`:

```ts
    this.rich.packId.set(a.packId);
    this.rich.version.set(a.version);
```

6. Replace `describe` and add `plainPrompt`:

```ts
  plainPrompt(q: Question) {
    return plainBlocks(q.prompt);
  }
  describe(q: Question, a?: Answer): string {
    if (!a) return 'Unanswered';
    const text = (option?: Option) => (option ? plainInlines(option.text) : undefined);
    if (q.kind === 'single' || q.kind === 'multiple' || q.kind === 'sequence')
      return (
        a.selected.map((id) => text(q.options.find((o) => o.id === id)) || id).join(q.kind === 'sequence' ? ' → ' : ', ') ||
        'Unanswered'
      );
    return q.slots
      .map((s) => plainInlines(s.text) + ': ' + (text((q.kind === 'matching' ? q.options : s.options).find((o) => o.id === a.slots[s.id])) || 'Unanswered'))
      .join(' · ');
  }
```

`expected(q: Question, g: Grade)` is unchanged.

- [ ] **Step 10: Render lessons with the new component**

In `apps/web/src/app/pages/course.ts`:

- Add imports `effect` (from `@angular/core`), `LessonBlockView` from `'../rich/lesson-block'` and `RichContext` from `'../rich/context'`. Add `LessonBlockView` to `imports` and `providers: [RichContext]` to the component.
- Replace the lesson block loop, from `@for (block of l.blocks; track $index) {` through its closing `}`, with:

```html
                  @for (block of l.blocks; track block.id) {
                    <lf-lesson-block [block]="block" [lessonId]="l.id" [parentLanguage]="c.language" />
                  }
```

- In the class, add:

```ts
  private readonly rich = inject(RichContext);

  constructor() {
    // The renderers read the course, release and glossary from the page context.
    effect(() => {
      const course = this.course();
      this.rich.packId.set(this.id());
      this.rich.version.set(course?.version ?? '');
      this.rich.glossary.set(course?.glossary ?? []);
    });
  }
```

- [ ] **Step 11: Add styles**

Append to `apps/web/src/styles.scss`:

```scss
lf-blocks {
  display: block;
}
.question-card h2.eyebrow {
  margin: 0;
  font-size: 0.65rem;
}
.question-prompt {
  margin: 28px 0;
  font-size: 1.2rem;
  color: var(--ink);
}
.review-prompt {
  margin-bottom: 12px;
}
.term {
  border: 0;
  border-bottom: 2px dotted var(--green);
  background: none;
  padding: 0;
  color: inherit;
  font: inherit;
  cursor: pointer;
}
.term[aria-expanded='true'] {
  background: var(--lime);
}
.term-definition {
  display: block;
  margin: 6px 0 10px;
  padding: 8px 12px;
  border-left: 3px solid var(--green);
  background: var(--white);
}
.code-region,
.table-region {
  max-width: 100%;
  overflow-x: auto;
}
.code-region pre {
  white-space: pre;
  overflow-wrap: normal;
}
.code-language {
  display: block;
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--green);
  margin-bottom: 4px;
}
.code-region:focus-visible,
.table-region:focus-visible,
.content-block:focus-visible {
  outline: 3px solid #c47736;
  outline-offset: 3px;
}
.table-region table {
  border-collapse: collapse;
  min-width: 100%;
}
.table-region caption {
  text-align: left;
  font-weight: 700;
  padding: 6px 0;
  color: var(--ink);
}
.table-region th,
.table-region td {
  border: 1px solid var(--line);
  padding: 8px 12px;
  text-align: left;
}
.table-region thead th,
.table-region th[scope='row'] {
  background: var(--lime);
  color: var(--ink);
}
.math-figure {
  margin: 12px 0;
}
.math-figure lf-math {
  display: block;
  overflow-x: auto;
}
.math-figure figcaption,
.primary-source figcaption {
  font-size: 0.85rem;
  color: var(--muted);
}
.block-label {
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 1px;
  text-transform: uppercase;
  color: var(--green);
  margin: 14px 0 4px;
}
.primary-source blockquote {
  margin: 0 0 8px;
  padding-left: 16px;
  border-left: 3px solid var(--line);
  font-style: italic;
}
.content-block.inlineCheck,
.content-block.misconception {
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 20px 24px;
}
```

- [ ] **Step 12: Run the web tests and build**

Run: `npm test --prefix apps/web && npm run build --prefix apps/web`
Expected: every Vitest spec passes and the production build succeeds.

- [ ] **Step 13: Commit**

```bash
git add apps/web
git commit -m "feat(web): render rich content, math and inline checks as data

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 14: Confirm the committed types match the API**

Run: `make check-api-types`
Expected: exit code 0 and no diff. The command regenerates the types and compares them with the index, so it only passes once they are committed.

---

### Task 14: Course page: modules, glossary, language and deep links

**Files:**
- Modify: `apps/web/src/app/pages/course.ts`, `apps/web/src/styles.scss`
- Test: `apps/web/src/app/pages/course.spec.ts`

**Interfaces:**
- Consumes: `LessonBlockView`, `Inlines`, `RichContext` (Task 13); `CourseCatalogDto.modules`, `.glossary`, `.language` and `.direction`.
- Produces: a `block` route query input. Selecting `?tab=learn&lesson=<id>&block=<id>` focuses `#block-<lesson>-<block>`. The Glossary tab appears when the course has glossary entries. The lesson navigation is grouped under module headings.

- [ ] **Step 1: Write the failing test**

`apps/web/src/app/pages/course.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { CoursePage } from './course';

const text = (value: string) => [{ kind: 'text', text: value }];
const lesson = (id: string, title: string, minutes: number | null) => ({
  id, title, summary: `${title} summary`, objectiveIds: ['o'], minutes, language: null, direction: null,
  blocks: [{ kind: 'text', id: 'b1', title: null, language: null, direction: null, body: [{ kind: 'paragraph', inlines: text(title) }] }],
});
const course = {
  id: 'demo', title: 'Démo', description: 'Un cours', version: '1.0.0', license: 'CC0', language: 'fr', direction: 'ltr',
  catalog: { subject: 'Maths', level: 'introductory', tags: [], estimatedHours: 1 },
  objectives: [{ id: 'o', title: 'Objective', prerequisites: [] }],
  modules: [{ id: 'm1', title: 'Premier', lessonIds: ['a'] }, { id: 'm2', title: 'Second', lessonIds: ['b'] }],
  glossary: [{ id: 'z', term: 'zèbre', definition: text('un animal') }, { id: 'a', term: 'abeille', definition: text('un insecte') }],
  lessons: [lesson('a', 'Alpha', 12), lesson('b', 'Beta', null)],
  blueprints: [], sources: [], readiness: { shortAttempts: 5, fullAttempts: 3, threshold: 90, lookbackDays: 90, minimumFreshPercent: 100 },
  goal: 'readiness', questionCount: 0,
};

describe('course page', () => {
  it('groups lessons by module, marks content language and offers a glossary tab', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoursePage);
    fixture.componentRef.setInput('id', 'demo');
    TestBed.tick();
    TestBed.inject(HttpTestingController).expectOne('/api/catalog/demo').flush(course);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect([...element.querySelectorAll('.module-title')].map((h) => h.textContent)).toEqual(['Premier', 'Second']);
    expect(element.querySelector('.lesson-nav')!.textContent).toContain('12 min');
    expect(element.querySelector('h1')!.getAttribute('lang')).toBe('fr');
    expect(element.querySelector('.lesson-content')!.getAttribute('lang')).toBe('fr');
    expect([...element.querySelectorAll('[role="tab"]')].map((t) => t.textContent?.trim())).toContain('Glossary');
    expect(fixture.componentInstance.glossary().map((g) => g.term)).toEqual(['abeille', 'zèbre']);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test --prefix apps/web`
Expected: FAIL (`.module-title` not found, `glossary` is not a function).

- [ ] **Step 3: Implement**

In `apps/web/src/app/pages/course.ts`:

1. Add `afterNextRender`, `Injector` (from `@angular/core`) and `Inlines` (from `'../rich/inlines'`) to the imports. Add `Inlines` to the component `imports`.

2. Heading: replace `<h1>{{ c.title }}</h1>` and `<p class="lead">{{ c.description }}</p>` with:

```html
          <h1 [attr.lang]="c.language" [attr.dir]="rtl(c.direction)">{{ c.title }}</h1>
          <p class="lead" [attr.lang]="c.language" [attr.dir]="rtl(c.direction)">{{ c.description }}</p>
```

3. Tabs: change `@for (t of tabs; track t.id)` to `@for (t of tabs(); track t.id)`.

4. Lesson navigation: replace the `<nav class="panel lesson-nav" …>…</nav>` element with:

```html
              <nav class="panel lesson-nav" aria-label="Course lessons">
                @for (group of lessonGroups(); track $index) {
                  @if (group.title) {
                    <h3 class="module-title" [attr.lang]="c.language">{{ group.title }}</h3>
                  }
                  @for (item of group.lessons; track item.lesson.id) {
                    @let l = item.lesson;
                    <button [class.active]="current()?.id === l.id" (click)="lessonId.set(l.id)">
                      <span>{{ completed().has(l.id) ? '✓' : item.number.toString().padStart(2, '0') }}</span>
                      <div>
                        <strong [attr.lang]="c.language">{{ l.title }}</strong><small [attr.lang]="c.language">{{ l.summary }}</small>
                        @if (l.minutes) {
                          <small>{{ l.minutes }} min</small>
                        }
                        @if (revised().has(l.id)) {
                          <small class="pill subtle">Updated since you read it</small>
                        }
                      </div>
                    </button>
                  }
                }
              </nav>
```

5. Lesson article: wrap the title, summary and blocks:

```html
                <article class="panel reading">
                  <p class="eyebrow">BUILD YOUR UNDERSTANDING</p>
                  <div class="lesson-content" [attr.lang]="lessonLanguage()" [attr.dir]="lessonDirection()">
                    <h2>{{ l.title }}</h2>
                    <p class="lead">{{ l.summary }}</p>
                    @for (block of l.blocks; track block.id) {
                      <lf-lesson-block [block]="block" [lessonId]="l.id" [parentLanguage]="lessonLanguage()" />
                    }
                  </div>
```

   The existing `<div class="row">` with the completion button follows, unchanged.

6. Glossary panel: insert before `<div ngTabPanel value="practice" …>`:

```html
        @if (c.glossary.length) {
          <div ngTabPanel value="glossary" class="tab-panel">
            <ng-template ngTabContent>
              <div class="panel">
                <h2>Glossary</h2>
                <dl class="glossary" [attr.lang]="c.language" [attr.dir]="rtl(c.direction)">
                  @for (entry of glossary(); track entry.id) {
                    <div [id]="'term-' + entry.id">
                      <dt>{{ entry.term }}</dt>
                      <dd><lf-inlines [nodes]="entry.definition" /></dd>
                    </div>
                  }
                </dl>
              </div>
            </ng-template>
          </div>
        }
```

7. Class changes. Replace the `tabs` field with a computed signal and add the helpers:

```ts
  readonly block = input<string>();
  private readonly injector = inject(Injector);

  readonly tabs = computed(() => [
    { id: 'learn', title: 'Lessons' },
    { id: 'map', title: 'Content map' },
    ...(this.course()?.glossary.length ? [{ id: 'glossary', title: 'Glossary' }] : []),
    { id: 'practice', title: 'Practice & exams' },
    { id: 'sources', title: 'References' },
  ]);
  // Modules set reading order; courses without modules show one ungrouped list. Numbers run across modules.
  readonly lessonGroups = computed(() => {
    const c = this.course();
    if (!c) return [];
    const numbered = new Map(c.lessons.map((lesson, i) => [lesson.id, { lesson, number: i + 1 }] as const));
    if (!c.modules.length) return [{ title: null as string | null, lessons: [...numbered.values()] }];
    return c.modules.map((m) => ({
      title: m.title as string | null,
      lessons: m.lessonIds.map((id) => numbered.get(id)).filter((item) => item !== undefined),
    }));
  });
  readonly glossary = computed(() => {
    const c = this.course();
    return [...(c?.glossary ?? [])].sort((a, b) => a.term.localeCompare(b.term, c?.language ?? undefined));
  });
  readonly lessonLanguage = computed(() => this.current()?.language ?? this.course()?.language ?? null);
  readonly lessonDirection = computed(() => {
    const lesson = this.current();
    return this.rtl(lesson?.language ? lesson.direction : this.course()?.direction);
  });
  rtl(direction: string | null | undefined) {
    return direction === 'rtl' ? 'rtl' : null;
  }
```

   Extend the constructor so a `block` query parameter focuses its block after rendering:

```ts
    // Lesson links carry ?lesson=…&block=…; move focus to the linked block once it renders.
    effect(() => {
      const blockId = this.block();
      const lesson = this.current();
      if (!blockId || !lesson) return;
      afterNextRender(() => document.getElementById(`block-${lesson.id}-${blockId}`)?.focus(), { injector: this.injector });
    });
```

8. Styles, appended to `styles.scss`:

```scss
.module-title {
  font-size: 0.72rem;
  letter-spacing: 1px;
  text-transform: uppercase;
  color: var(--green);
  margin: 16px 12px 6px;
}
.glossary > div {
  padding: 10px 0;
  border-bottom: 1px solid var(--line);
}
.glossary dt {
  font-weight: 700;
  color: var(--ink);
}
.glossary dd {
  margin: 4px 0 0;
}
```

- [ ] **Step 4: Run the tests and build**

Run: `npm test --prefix apps/web && npm run build --prefix apps/web`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web
git commit -m "feat(web): course modules, glossary tab, content language and block deep links

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Library metadata and filters

**Files:**
- Modify: `apps/web/src/app/pages/courses.ts`, `apps/web/src/styles.scss`
- Test: `apps/web/src/app/pages/courses.spec.ts`

**Interfaces:**
- Consumes: `CourseCard` (`CatalogSummaryDto`, including `subject`, `level`, `language`, `tags` and `estimatedHours`).
- Produces: a library with labelled Subject, Level and Language selects, and a polite status reading "Showing N of M courses".

- [ ] **Step 1: Write the failing test**

`apps/web/src/app/pages/courses.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { CoursesPage } from './courses';

const card = (id: string, subject: string | null, level: string | null, language: string | null) => ({
  id, title: id, description: `${id} course`, version: '1.0.0', lessonCount: 1, questionCount: 1, objectiveCount: 1,
  language, direction: 'ltr', subject, level, tags: [], estimatedHours: subject ? 2 : null,
});

describe('learning library', () => {
  it('filters by subject, level and language and reports the result count', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoursesPage);
    TestBed.inject(HttpTestingController)
      .expectOne('/api/catalog')
      .flush([card('maths', 'Mathematics', 'introductory', 'en'), card('french', 'Languages', 'intermediate', 'fr'), card('legacy', null, null, null)]);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const status = () => element.querySelector('[role="status"]')!.textContent!.trim();
    const select = (label: string) => [...element.querySelectorAll('label')].find((l) => l.textContent?.trim().startsWith(label))!.querySelector('select')!;
    expect(status()).toBe('Showing 3 of 3 courses');
    expect(element.textContent).toContain('French');
    expect(element.textContent).toContain('2 hours');

    select('Subject').value = 'Languages';
    select('Subject').dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(status()).toBe('Showing 1 of 3 courses');

    select('Subject').value = '';
    select('Subject').dispatchEvent(new Event('change'));
    select('Language').value = 'en';
    select('Language').dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect([...element.querySelectorAll('.course-card h2')].map((h) => h.textContent)).toEqual(['maths']);

    select('Language').value = '';
    select('Language').dispatchEvent(new Event('change'));
    select('Level').value = 'intermediate';
    select('Level').dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(status()).toBe('Showing 1 of 3 courses');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test --prefix apps/web`
Expected: FAIL (no `role="status"` element).

- [ ] **Step 3: Implement**

Replace `apps/web/src/app/pages/courses.ts`:

```ts
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { CourseCard, CourseLevel } from '../models';

const levels: { value: CourseLevel; label: string }[] = [
  { value: 'introductory', label: 'Introductory' },
  { value: 'intermediate', label: 'Intermediate' },
  { value: 'advanced', label: 'Advanced' },
];
const languageNames = new Intl.DisplayNames(['en'], { type: 'language' });

@Component({
  imports: [RouterLink, FormsModule],
  template: ` <div class="page-heading">
      <div>
        <p class="eyebrow">A WORLD OF UNDERSTANDING</p>
        <h1>Learning library<span class="accent">.</span></h1>
        <p class="lead">Find a subject. Follow its connections. Put your knowledge to work.</p>
      </div>
      <span class="pill">{{ courses().length }} courses</span>
    </div>
    <div class="library-filters">
      <label class="search"
        >Search your library<input
          type="search"
          placeholder="Search a subject or topic"
          [ngModel]="query()"
          (ngModelChange)="query.set($event)"
      /></label>
      <label
        >Subject<select [ngModel]="subject()" (ngModelChange)="subject.set($event)">
          <option value="">All subjects</option>
          @for (s of subjects(); track s) {
            <option [value]="s">{{ s }}</option>
          }
        </select></label
      >
      <label
        >Level<select [ngModel]="level()" (ngModelChange)="level.set($event)">
          <option value="">All levels</option>
          @for (l of levels; track l.value) {
            <option [value]="l.value">{{ l.label }}</option>
          }
        </select></label
      >
      <label
        >Language<select [ngModel]="language()" (ngModelChange)="language.set($event)">
          <option value="">All languages</option>
          @for (tag of languages(); track tag) {
            <option [value]="tag">{{ languageName(tag) }}</option>
          }
        </select></label
      >
    </div>
    <p class="muted small" role="status">
      {{ loaded() ? 'Showing ' + filtered().length + ' of ' + courses().length + ' courses' : '' }}
    </p>
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    }
    <div class="course-grid">
      @for (course of filtered(); track course.id; let i = $index) {
        <a class="course-card" [routerLink]="['/courses', course.id]"
          ><div class="course-art" [class.alternate]="i % 2">
            <span class="course-number">0{{ i + 1 }}</span>
            <div class="art-orbit"></div>
            <span class="tiny-label">LEARNING PATH</span>
          </div>
          <div class="course-card-body">
            <div class="row">
              <span class="eyebrow">{{ course.objectiveCount }} CONNECTED OBJECTIVES</span><span aria-hidden="true">↗</span>
            </div>
            <h2 [attr.lang]="course.language">{{ course.title }}</h2>
            <p [attr.lang]="course.language">{{ course.description }}</p>
            <div class="card-meta">
              @if (course.subject) {
                <span>{{ course.subject }}</span>
              }
              @if (course.level) {
                <span>{{ levelLabel(course.level) }}</span>
              }
              @if (course.language) {
                <span>{{ languageName(course.language) }}</span>
              }
              @if (course.estimatedHours) {
                <span>{{ course.estimatedHours }} hours</span>
              }
              <span>{{ course.lessonCount }} lessons</span><span>{{ course.questionCount }} practice questions</span>
            </div>
          </div></a
        >
      } @empty {
        <p class="empty">{{ loaded() ? 'No courses match these filters.' : 'Loading the library…' }}</p>
      }
    </div>`,
})
export class CoursesPage {
  private readonly api = inject(Api);
  readonly levels = levels;
  readonly courses = signal<CourseCard[]>([]);
  readonly query = signal('');
  readonly subject = signal('');
  readonly level = signal('');
  readonly language = signal('');
  readonly loaded = signal(false);
  readonly error = signal('');
  readonly subjects = computed(() => [...new Set(this.courses().flatMap((c) => (c.subject ? [c.subject] : [])))].sort());
  readonly languages = computed(() => [...new Set(this.courses().flatMap((c) => (c.language ? [c.language] : [])))].sort());
  // A course without metadata appears only when the matching filter is "All".
  readonly filtered = computed(() => {
    const query = this.query().toLowerCase();
    return this.courses().filter(
      (c) =>
        [c.title, c.description, c.subject ?? '', ...c.tags].join(' ').toLowerCase().includes(query) &&
        (!this.subject() || c.subject === this.subject()) &&
        (!this.level() || c.level === this.level()) &&
        (!this.language() || c.language === this.language()),
    );
  });
  constructor() {
    this.api
      .get<CourseCard[]>('/catalog')
      .then((c) => this.courses.set(c))
      .catch((e) => this.error.set(message(e)))
      .finally(() => this.loaded.set(true));
  }
  levelLabel(level: CourseLevel) {
    return levels.find((l) => l.value === level)?.label ?? level;
  }
  languageName(tag: string) {
    try {
      return languageNames.of(tag) ?? tag;
    } catch {
      return tag;
    }
  }
}
```

Append to `styles.scss`:

```scss
.library-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  align-items: flex-end;
  margin-bottom: 12px;
}
.library-filters .search {
  flex: 1 1 280px;
  margin-bottom: 0;
}
.library-filters label {
  flex: 0 1 180px;
}
```

- [ ] **Step 4: Run the tests and build**

Run: `npm test --prefix apps/web && npm run build --prefix apps/web`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web
git commit -m "feat(web): show catalog metadata and filter the library by subject, level and language

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: Studio diagnostics and learner preview

**Files:**
- Modify: `apps/web/src/app/pages/studio.ts`, `apps/web/src/styles.scss`
- Test: `apps/web/src/app/pages/studio.spec.ts`

**Interfaces:**
- Consumes: `POST /api/authoring/validate` → `ValidationResponseDto`; `POST /api/authoring/preview` → `PreviewDto` (Task 12); `LessonBlockView`, `Blocks`, `QuestionInput`, `RichContext` (Task 13).
- Produces: a Studio that announces the result count, moves focus to the diagnostics heading, lists code, path, message and guidance for each diagnostic, and previews the pack with learner components. Keys appear only in "Answer keys (authors only)".

- [ ] **Step 1: Write the failing test**

`apps/web/src/app/pages/studio.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { Api } from '../api';
import { User } from '../models';
import { StudioPage } from './studio';

const text = (value: string) => [{ kind: 'text', text: value }];
const preview = {
  course: {
    id: 'demo', title: 'Demo', description: 'Demo', version: '1.0.0', license: 'CC0', language: 'en', direction: 'ltr', catalog: null,
    objectives: [], modules: [], glossary: [], blueprints: [], sources: [], goal: 'readiness', questionCount: 1,
    readiness: { shortAttempts: 5, fullAttempts: 3, threshold: 90, lookbackDays: 90, minimumFreshPercent: 100 },
    lessons: [{ id: 'intro', title: 'Intro', summary: 'S', objectiveIds: [], minutes: null, language: null, direction: null,
      blocks: [{ kind: 'text', id: 'b1', title: null, language: null, direction: null, body: [{ kind: 'paragraph', inlines: text('Hello') }] }] }],
  },
  questions: [{
    question: { id: 'q1', kind: 'single', prompt: [{ kind: 'paragraph', inlines: text('Pick') }], objectiveIds: [],
      options: [{ id: 'a', text: text('A') }, { id: 'b', text: text('B') }], slots: [], selectCount: 1, reuse: false, scenarioId: null, weight: 1 },
    grading: { policy: 'exact', correct: ['a'], matches: null },
    explanation: [{ kind: 'paragraph', inlines: text('Because A.') }],
  }],
  checks: [],
};

async function setup() {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  TestBed.inject(Api).user.set({ id: 'u', displayName: 'Pat', email: 'pat@example.test', publisher: true } as User);
  const fixture = TestBed.createComponent(StudioPage);
  await fixture.whenStable();
  fixture.componentInstance.source.set('{"schemaVersion":2}');
  await fixture.whenStable();
  const element = fixture.nativeElement as HTMLElement;
  const button = (label: string) => [...element.querySelectorAll('button')].find((b) => b.textContent?.includes(label))!;
  return { fixture, element, button, http: TestBed.inject(HttpTestingController) };
}

describe('content studio', () => {
  it('announces diagnostics and moves focus to their summary', async () => {
    const { fixture, element, button, http } = await setup();
    button('Validate content').click();
    http.expectOne('/api/authoring/validate').flush({
      success: false, hash: 'h', lessonCount: null, questionCount: null,
      diagnostics: [{ code: 'A11Y_STRUCTURE', path: 'lessons.intro.blocks.b1.body:1:1', message: 'Headings are not supported inside content.', guidance: 'Give the block a title.' }],
    });
    await fixture.whenStable();
    const heading = element.querySelector('#diagnostics-heading') as HTMLElement;
    expect(heading.textContent).toBe('1 problem found');
    expect(document.activeElement).toBe(heading);
    expect(element.querySelector('[role="status"]')!.textContent).toBe('1 problem found');
    expect(element.querySelector('.diagnostic')!.textContent).toContain('lessons.intro.blocks.b1.body:1:1');
    expect(element.querySelector('.diagnostic')!.textContent).toContain('Give the block a title.');
  });

  it('previews with learner components and keeps answer keys in the author panel', async () => {
    const { fixture, element, button, http } = await setup();
    button('Validate content').click();
    http.expectOne('/api/authoring/validate').flush({ success: true, hash: 'h', lessonCount: 1, questionCount: 1, diagnostics: [] });
    await fixture.whenStable();
    button('Preview as a learner').click();
    http.expectOne('/api/authoring/preview').flush(preview);
    await fixture.whenStable();
    const learner = element.querySelector('.preview')!;
    expect(learner.querySelector('lf-lesson-block')!.textContent).toContain('Hello');
    expect(learner.textContent).not.toContain('Because A.');
    expect(element.querySelector('.author-keys')!.textContent).toContain('Key: a');
    expect(element.querySelector('.author-keys')!.textContent).toContain('Because A.');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test --prefix apps/web`
Expected: FAIL (`#diagnostics-heading` not found).

- [ ] **Step 3: Implement**

Replace `apps/web/src/app/pages/studio.ts`:

```ts
import { afterNextRender, Component, ElementRef, inject, Injector, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, message } from '../api';
import { Grading, PreviewDto, ValidationResponseDto } from '../models';
import { QuestionInput } from '../question-input';
import { Blocks } from '../rich/blocks';
import { RichContext } from '../rich/context';
import { LessonBlockView } from '../rich/lesson-block';

@Component({
  imports: [FormsModule, RouterLink, Blocks, LessonBlockView, QuestionInput],
  providers: [RichContext],
  template: ` <div class="page-heading">
      <div>
        <p class="eyebrow">BUILD SOMETHING WORTH LEARNING</p>
        <h1>Content studio<span class="accent">.</span></h1>
        <p class="lead">Create a subject pack from reusable templates. Check the connections before publishing.</p>
      </div>
    </div>
    @if (!api.user()?.publisher) {
      <p class="alert">Publisher access is required. Ask your LearnForge administrator to grant it to your account.</p>
    } @else {
      <div class="practice-layout">
        <section class="panel">
          <label
            >Import a JSON source file<input type="file" accept="application/json,.json" (change)="loadFile($event)" /></label
          ><label
            >Pack source<textarea rows="20" spellcheck="false" [ngModel]="source()" (ngModelChange)="edit($event)"></textarea>
          </label>
          <div class="row">
            <button class="button secondary" [disabled]="busy()" (click)="validate()">Validate content</button
            ><button class="button" [disabled]="busy() || !report()?.success" (click)="publish()">Publish new release</button>
          </div>
        </section>
        <aside>
          <div class="recommendation">
            <p class="eyebrow">ONE SOURCE, CONNECTED CONTENT</p>
            <h2>Write once.<br />Check the whole picture.</h2>
            <p>
              The same compiler checks Markdown and math, template expansion, answer keys, prerequisite cycles, objective
              coverage, language metadata and exam composition.
            </p>
            <p class="small">Published releases are immutable. Use a new version to update a course.</p>
          </div>
          @if (report(); as r) {
            <section class="panel diagnostics" aria-labelledby="diagnostics-heading">
              <h2 id="diagnostics-heading" tabindex="-1" #diagnosticsHeading>{{ summary(r) }}</h2>
              @if (r.success) {
                <p>{{ r.lessonCount }} lessons · {{ r.questionCount }} questions</p>
                <button class="button secondary" [disabled]="busy()" (click)="loadPreview()">Preview as a learner</button>
              }
              <ol class="diagnostic-list">
                @for (d of r.diagnostics; track $index) {
                  <li class="diagnostic">
                    <strong>{{ d.code }}</strong> <code>{{ d.path }}</code>
                    <p>{{ d.message }}</p>
                    @if (d.guidance) {
                      <p class="muted small">{{ d.guidance }}</p>
                    }
                  </li>
                }
              </ol>
            </section>
          }
        </aside>
      </div>
      @if (preview(); as p) {
        <section class="panel preview" aria-labelledby="preview-heading">
          <h2 id="preview-heading">Learner preview</h2>
          @for (lesson of p.course.lessons; track lesson.id) {
            <article class="reading" [attr.lang]="lesson.language ?? p.course.language">
              <h3>{{ lesson.title }}</h3>
              <p class="lead">{{ lesson.summary }}</p>
              @for (block of lesson.blocks; track block.id) {
                <lf-lesson-block [block]="block" [lessonId]="lesson.id" [parentLanguage]="lesson.language ?? p.course.language" />
              }
            </article>
          }
          <h3>Question bank</h3>
          @for (item of p.questions; track item.question.id) {
            <article class="preview-question" [attr.lang]="p.course.language">
              <div [id]="'preview-prompt-' + item.question.id"><lf-blocks [blocks]="item.question.prompt" /></div>
              <lf-question-input
                [question]="item.question"
                [instanceId]="'preview-' + item.question.id"
                [promptId]="'preview-prompt-' + item.question.id"
                [disabled]="true"
              />
            </article>
          }
        </section>
        <section class="panel author-keys" aria-labelledby="keys-heading">
          <h2 id="keys-heading">Answer keys (authors only)</h2>
          <dl>
            @for (item of p.questions; track item.question.id) {
              <dt>{{ item.question.id }}</dt>
              <dd>
                <p>Key: {{ key(item.grading) }}</p>
                <lf-blocks [blocks]="item.explanation" />
              </dd>
            }
            @for (check of p.checks; track check.lessonId + '#' + check.blockId) {
              <dt>{{ check.lessonId }}#{{ check.blockId }}</dt>
              <dd>
                <p>Key: {{ key(check.grading) }}</p>
                <lf-blocks [blocks]="check.explanation" />
              </dd>
            }
          </dl>
        </section>
      }
    }
    <p class="sr-only" role="status">{{ status() }}</p>
    @if (error()) {
      <p class="alert error" role="alert">{{ error() }}</p>
    }
    @if (published()) {
      <p class="alert success">Release published. <a routerLink="/courses">Open learning library →</a></p>
    }`,
})
export class StudioPage {
  readonly api = inject(Api);
  private readonly rich = inject(RichContext);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('diagnosticsHeading');
  readonly source = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly status = signal('');
  readonly published = signal(false);
  readonly report = signal<ValidationResponseDto | null>(null);
  readonly preview = signal<PreviewDto | null>(null);

  constructor() {
    // An unpublished pack has no course page: links render as text and inline checks cannot be answered.
    this.rich.interactive.set(false);
  }
  summary(r: ValidationResponseDto) {
    const count = r.diagnostics.length;
    return r.success ? 'No problems found. Ready to publish.' : `${count} ${count === 1 ? 'problem' : 'problems'} found`;
  }
  key(grading: Grading) {
    return grading.correct.length
      ? grading.correct.join(', ')
      : Object.entries(grading.matches ?? {})
          .map(([slot, option]) => `${slot} → ${option}`)
          .join(', ');
  }
  edit(value: string) {
    this.source.set(value);
    this.report.set(null);
    this.preview.set(null);
  }
  async loadFile(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (file.size > 2_000_000) {
      this.error.set('Source files must be under 2 MB.');
      return;
    }
    this.edit(await file.text());
  }
  async validate() {
    this.busy.set(true);
    this.error.set('');
    this.published.set(false);
    this.preview.set(null);
    try {
      const report = await this.api.post<ValidationResponseDto>('/authoring/validate', JSON.parse(this.source()));
      this.report.set(report);
      this.status.set(this.summary(report));
      // Keyboard and screen-reader users land on the result instead of hunting for it.
      afterNextRender(() => this.heading()?.nativeElement.focus(), { injector: this.injector });
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  async loadPreview() {
    this.busy.set(true);
    this.error.set('');
    try {
      const preview = await this.api.post<PreviewDto>('/authoring/preview', JSON.parse(this.source()));
      this.rich.glossary.set(preview.course.glossary);
      this.preview.set(preview);
      this.status.set(`Preview ready: ${preview.course.lessons.length} lessons and ${preview.questions.length} questions.`);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
  async publish() {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.post('/authoring/publish', JSON.parse(this.source()));
      this.published.set(true);
      this.status.set('Release published.');
      this.report.set(null);
      this.preview.set(null);
    } catch (e) {
      this.error.set(message(e));
    } finally {
      this.busy.set(false);
    }
  }
}
```

Append to `styles.scss`:

```scss
.diagnostic-list {
  padding-left: 20px;
}
.diagnostic code {
  overflow-wrap: anywhere;
}
.preview,
.author-keys {
  margin-top: 26px;
}
.preview-question {
  border-top: 1px solid var(--line);
  padding: 16px 0;
}
.author-keys dt {
  font-weight: 700;
  color: var(--ink);
  margin-top: 12px;
}
```

- [ ] **Step 4: Run the tests and build**

Run: `npm test --prefix apps/web && npm run build --prefix apps/web`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web
git commit -m "feat(web): focus Studio diagnostics and preview packs with the learner renderer

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Browser journeys and automated accessibility scans

**Files:**
- Modify: `apps/web/package.json`, `apps/web/package-lock.json` (via npm)
- Create: `apps/web/e2e/rich-content.spec.ts`

**Interfaces:**
- Consumes: the running app (`make dev`), the demo pack from Task 10, and `apps/api/bin/Debug/net10.0/LearnForge.Api.dll` (built by `make dev`), used for `--grant-publisher`.
- Produces: Playwright coverage for signed-out rich reading, keyboard inline checks, lesson links, the glossary, library filters, a math prompt in an attempt, the Studio flow, and axe scans (WCAG 2.0/2.1/2.2 A/AA tags) of each page.

- [ ] **Step 1: Add the axe integration**

```bash
npm install --save-dev @axe-core/playwright --prefix apps/web
```

- [ ] **Step 2: Write the journeys**

`apps/web/e2e/rich-content.spec.ts`:

```ts
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

async function expectNoAxeViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze();
  expect(results.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`)).toEqual([]);
}

async function register(page: Page, name: string) {
  const email = `rich-${Date.now()}-${Math.random().toString(36).slice(2)}@example.test`;
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill(name);
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: `Welcome back, ${name}.` })).toBeVisible();
  return email;
}

// Grants Publisher with the API's own maintenance command against the development database that `make dev` uses.
function grantPublisher(email: string) {
  const api = resolve(__dirname, '../../api');
  execFileSync('dotnet', [resolve(api, 'bin/Debug/net10.0/LearnForge.Api.dll'), '--grant-publisher', email], {
    cwd: api,
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
    stdio: 'pipe',
  });
}

test('a signed-out reader explores rich lessons and checks an answer with the keyboard', async ({ page }) => {
  await page.goto('/courses/reasoning-foundations');
  const lesson = page.locator('.lesson-content');
  await expect(lesson.getByRole('heading', { name: 'Think in sets' })).toBeVisible();
  await expect(lesson.locator('math').first()).toBeVisible();
  await expect(lesson.locator('caption')).toHaveText('Two sets and their combinations');
  await expect(lesson.locator('tbody th[scope="row"]').first()).toHaveText('A');

  const term = lesson.getByRole('button', { name: 'set', exact: true });
  await term.focus();
  await page.keyboard.press('Enter');
  await expect(term).toHaveAttribute('aria-expanded', 'true');
  await expect(lesson.getByText('A collection of distinct elements, where order does not matter.')).toBeVisible();

  const check = page.locator('#block-sets-intro-check-intersection');
  await check.getByRole('radio').first().focus();
  await page.keyboard.press('Space');
  const button = check.getByRole('button', { name: 'Check answer' });
  await button.focus();
  await page.keyboard.press('Enter');
  await expect(check.locator('.check-status')).toHaveText('Correct.');
  await expect(button).toBeFocused();
  await expect(check.locator('.feedback')).toContainText('only 2 appears in both');
  await expectNoAxeViolations(page);
});

test('lesson links move focus to the linked block and the glossary lists every term', async ({ page }) => {
  await page.goto('/courses/reasoning-foundations?tab=learn&lesson=logic-intro');
  const lesson = page.locator('.lesson-content');
  await expect(lesson.locator('[lang="la"]')).toHaveText('modus ponens');
  await expect(lesson.locator('math[display="block"]')).toBeVisible();
  await expect(lesson.locator('blockquote')).toContainText('The design of the following treatise');
  await lesson.getByRole('link', { name: 'union and intersection' }).click();
  await expect(page.locator('#block-sets-intro-union-and-intersection')).toBeFocused();
  await page.getByRole('tab', { name: 'Glossary' }).click();
  await expect(page.getByRole('term').filter({ hasText: 'intersection' })).toBeVisible();
  await expectNoAxeViolations(page);
});

test('the library filters by subject and language', async ({ page }) => {
  await page.goto('/courses');
  await expect(page.getByRole('status')).toContainText('Showing');
  await page.getByLabel('Subject').selectOption('Mathematics');
  await expect(page.getByRole('heading', { name: 'Reasoning foundations' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Evidence lab' })).toHaveCount(0);
  await page.getByLabel('Subject').selectOption('');
  await expect(page.getByRole('heading', { name: 'Evidence lab' })).toBeVisible();
  await page.getByLabel('Language').selectOption('en');
  await expect(page.getByRole('heading', { name: 'Evidence lab' })).toHaveCount(0);
  await expectNoAxeViolations(page);
});

test('attempt questions render math and pass an accessibility scan', async ({ page }) => {
  await register(page, 'Mo');
  await page.goto('/courses/reasoning-foundations?tab=practice');
  await page.getByLabel('Feedback mode').selectOption('learn');
  await page.getByRole('button', { name: 'Begin session' }).click();
  await expect(page.locator('lf-question-input')).toBeVisible();
  // Every short session includes a dropdown question, whose blanks are labelled with math.
  for (let n = 0; n < 5; n++) {
    const card = page.locator('.question-card');
    if ((await card.locator('.pill').innerText()) === 'Dropdown blanks') {
      await expect(card.locator('label math').first()).toBeVisible();
      await expectNoAxeViolations(page);
      return;
    }
    await page.getByRole('button', { name: 'Next →', exact: true }).click();
  }
  throw new Error('The short session had no dropdown question.');
});

test('a publisher validates, previews and sees answer keys only in the author panel', async ({ page }) => {
  test.setTimeout(120_000);
  const email = await register(page, 'Pat');
  grantPublisher(email);
  await page.getByRole('button', { name: 'Sign out' }).click();
  await page.goto('/sign-in');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('link', { name: 'Content studio' }).click();

  const source = JSON.parse(readFileSync(resolve(__dirname, '../../../packs/reasoning-foundations.json'), 'utf8'));
  source.pack.lessons[0].blocks[0].body = '# A heading';
  await page.getByLabel('Pack source').fill(JSON.stringify(source));
  await page.getByRole('button', { name: 'Validate content' }).click();
  await expect(page.getByRole('heading', { name: '1 problem found' })).toBeFocused();
  await expect(page.locator('.diagnostic').first()).toContainText('lessons.sets-intro.blocks.what-is-a-set.body:1:1');

  source.pack.lessons[0].blocks[0].body = 'A set is a collection of distinct elements.';
  await page.getByLabel('Pack source').fill(JSON.stringify(source));
  await page.getByRole('button', { name: 'Validate content' }).click();
  await expect(page.getByRole('heading', { name: 'No problems found. Ready to publish.' })).toBeFocused();
  await page.getByRole('button', { name: 'Preview as a learner' }).click();
  await expect(page.getByRole('heading', { name: 'Learner preview' })).toBeVisible();
  await expect(page.locator('.preview')).not.toContainText('Key:');
  await expect(page.locator('.author-keys')).toContainText('sets-intro#check-intersection');
  await expectNoAxeViolations(page);
});
```

- [ ] **Step 3: Run the browser suite**

Start the app in one terminal with `make dev`, then run:

```bash
cd apps/web && npx playwright test
```

Expected: every test in `learning.spec.ts` and `rich-content.spec.ts` passes.

If an axe scan reports a violation in a component this feature did not add (for example a pre-existing contrast issue on a page it touches), fix it in this task. The accessibility baseline puts every full page in scope. Name each fix in the commit message. Do not disable axe rules or narrow the scan scope to get a pass.

- [ ] **Step 4: Commit**

```bash
git add apps/web
git commit -m "test(e2e): cover rich lessons, inline checks, studio preview and axe scans

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 18: Documentation and final verification

**Files:**
- Modify: `docs/authoring.md`, `docs/content-engine.md`, `docs/architecture.md`, `docs/api.md`, `docs/user-guide.md`, `docs/testing.md`, `CLAUDE.md`, `docs/accessibility/content-and-authoring.md`, `docs/superpowers/specs/2026-09-25-learner-roadmap.md`, `docs/roadmap.md`

**Interfaces:** documentation only; it must describe exactly what Tasks 1–17 built.

- [ ] **Step 1: Authoring guide**

In `docs/authoring.md`, replace the second sentence of the opening accessibility paragraph with:

> Schema 2 packs get machine checks for language, structure and math and table alternatives (`A11Y_LANGUAGE`, `A11Y_STRUCTURE`, `A11Y_ALTERNATIVE`). Figures, media and the review-record publishing gate are planned; until they ship, every release also needs the manual accessibility review.

Then add this section after "Build a pack":

````markdown
## Schema 2: rich content

Set `"schemaVersion": 2` to write Markdown and math. Schema 1 packs still compile, and their text stays literal. Convert one with `dotnet run --project tools/cli -- upgrade old.json --language en --out new.json`, which escapes Markdown characters, names blocks `b1`, `b2`… and adds a catalog stub to review.

### Pack metadata

| Field | Rule |
| --- | --- |
| `language` | Required BCP 47 tag in canonical case: `en`, `pt-BR`, `zh-Hant`, `es-419` |
| `direction` | `ltr` (default) or `rtl`; must match the language's script |
| `catalog` | `{ "subject", "level": "introductory" \| "intermediate" \| "advanced", "tags"?, "estimatedHours"? }` |
| `modules` | Optional `[{ "id", "title", "lessonIds" }]`; every lesson in exactly one module; module order is reading order |
| `glossary` | Optional `[{ "id", "term", "definition" }]`; `definition` is inline Markdown |

Lessons accept `minutes` (1–600) and a `language` override. Every block needs an `id` that is unique within its lesson; links and focus use it.

### Markdown

Block fields (block bodies, prompts, explanations, scenario backgrounds, worked-example parts, misconceptions, primary-source excerpts and translations) accept paragraphs, lists nested up to 3 levels, quotes and fenced code with a language label. Inline fields (options, slot text, glossary definitions, table cells) accept one paragraph of emphasis, strong, inline code, hard breaks and `$…$` math. Write a literal dollar as `\$`. Dropdown options accept plain text only, because they render in a native select.

| Link | Meaning |
| --- | --- |
| `[text](https://…)` | External link, HTTPS only |
| `[text](lesson:lesson-id)` or `lesson:lesson-id#block-id` | Another lesson or block |
| `[text](#block-id)` | A block in the same lesson |
| `[term](term:glossary-id)` | A glossary term; learners can reveal its definition |
| `[text](lang:fr)` | Text in another language |

The compiler rejects raw HTML, images, headings (give the block a title instead), horizontal rules, indented code, `$$…$$` (use a math block), bare links, empty link text and other link schemes.

### Lesson blocks

| Kind | Fields |
| --- | --- |
| `text`, `callout`, `example` | `body`, optional `title` |
| `code` | `language` (programming language, e.g. `python`), `code` (literal) |
| `math` | `tex`, `description` (a required plain-language equivalent) |
| `table` | `caption`, `columns`, `rows`, optional `rowHeaders`; one cell per column, no merged cells |
| `workedExample` | `problem`, `steps` (1–30), `result` |
| `misconception` | `claim`, `correction` |
| `definition` | `termId`, optional `body`; the glossary term is its title |
| `primarySource` | `excerpt`, `attribution { title, author?, date?, url? }`, optional `language` and `translation` (a translation requires `language`) |
| `inlineCheck` | `question`, using the bank question contract without id, family, objectives, scenario or weight |

Inline checks are formative. The server grades them, keeps their keys out of lessons, and records no evidence.

### Math

Math uses a TeX subset:
- letters, numbers and operators, `^`, `_` and groups;
- `\frac`, `\sqrt[n]{}`, `\left…\right`;
- Greek letters, relations, set, logic and arrow symbols;
- `\sum`, `\prod`, `\int`, `\lim` with limits, and named functions;
- `\text`, `\mathrm`, `\mathbf`, `\mathit`, `\mathbb`, `\mathcal`;
- accents and spacing;
- `matrix`, `pmatrix`, `bmatrix`, `vmatrix`, `cases` and `aligned`.

Anything else is `LF301` with its position. The complete command list lives in `src/LearnForge.Core/Services/Content/TexSymbols.cs`.

### Diagnostics

| Code | Meaning |
| --- | --- |
| `LF002` | Duplicate JSON property |
| `LF201`–`LF204` | Unsupported Markdown, link, unresolved reference, or limit (20,000 characters per field, nesting 8, 200,000 nodes per pack) |
| `LF301`–`LF303` | Unsupported TeX, unbalanced group, TeX limit (2,000 characters, nesting 16) |
| `A11Y_LANGUAGE` | Invalid language tag, direction mismatch, or a translation without a language |
| `A11Y_STRUCTURE` | Duplicate block IDs, headings in content, table row shape, empty link text |
| `A11Y_ALTERNATIVE` | Math block without a description, table without a caption |

Paths name the field and position, e.g. `lessons.sets-intro.blocks.union.problem:1:12`. Each diagnostic includes guidance on how to fix it.
````

- [ ] **Step 2: Content engine, architecture and API docs**

`docs/content-engine.md`:
- In "Source shape", append: "Schema 2 adds `language`, `direction`, `catalog`, `modules`, `glossary`, lesson `minutes` and block IDs, and turns text fields into restricted Markdown (see the authoring guide)."
- Replace the paragraph starting "The current engine does not fetch files" with: "The engine does not fetch files or URLs, execute expressions or render HTML. Schema 2 text is parsed with a restricted Markdown and TeX subset into a typed AST (Markdig parses; its HTML renderer is never used). Raw HTML, images and unknown syntax are diagnostics. YAML, QTI and other adapters remain roadmap items."
- In "Validation", add: "duplicate JSON properties (`LF002`), Markdown and TeX syntax and limits, language tags and direction, modules, glossary terms, lesson and block links, table shape, and math and table alternatives."
- In "Build artifacts", add: "`grading.private.json` holds `questions` and inline-check `checks`; `search.json` text is flattened from the AST."
- Add a section:

```markdown
## Schema 1 compatibility

Schema 1 sources compile through frozen `V1*` records and `V1Upcaster`: text becomes literal text nodes, never Markdown, and blocks are named `b1`, `b2`… by position. Stored releases carry `format: 2` and precomputed lesson hashes; rows stored earlier have no format and are upcast on read by `ReleaseReader`, never rewritten. Schema 1 lesson hashes are computed on the v1 records, so republishing a v1 pack does not flag completed lessons as revised.
```

- Change the last sentence of "Cross-validation workflow" to: "Semantic duplicate detection and QTI import are future work; Content Studio previews a pack with the learner renderer before publishing."

`docs/architecture.md`:
- In "Core contracts", replace "Lessons use text, callout, example and code blocks." with: "Lessons use eleven block kinds (text, callout, example, code, math, table, worked example, misconception, definition, primary source and inline check). Rich fields hold a typed AST (`RichBlock`, `RichInline`, `MathNode`) with a `kind` discriminator. Inline-check keys live in `Pack.Checks`, so lessons are always safe to deliver."
- Add after "Core contracts":

```markdown
## Rich content pipeline

`ContentEngine` reads schema 2 sources into `Source*` records, and `PackCompiler` parses every rich field once: `RichTextCompiler` maps a restricted Markdig parse onto the AST, and `TexParser` turns TeX into MathML-shaped nodes. `RichContentRules` validates language, modules, glossary, references and accessibility alternatives on the compiled release. The API serves the AST as data. The Angular renderers in `src/app/rich/*` turn it into elements and native MathML with bindings only, never `innerHTML`.
```

- In "Persistence model", replace "PackReleases stores immutable compiled JSON plus a content hash." with: "PackReleases stores immutable compiled JSON (format 2: rich-text AST plus lesson hashes; older rows are upcast on read) and a source hash."

`docs/api.md`:
- Replace the "Public" paragraph with: "GET /health checks database connectivity. GET /api/catalog returns release summaries with language, direction, subject, level, tags and estimated hours. GET /api/catalog/{packId} returns teaching-safe course data: objectives, modules, glossary, lessons as a typed rich-text AST, blueprints, references and the goal. It never returns questions, keys or personal progress. POST /api/catalog/{packId}/checks with `{ version, lessonId, blockId, answer }` grades an inline check on the server and returns `{ grade, explanation }`. It allows anonymous callers, requires the CSRF header, is limited to 60 requests per minute and records nothing."
- In "Learner", replace "Completed attempts include released grades and explanations." with: "Released feedback and results are `{ grade, explanation }` objects; explanations are rich-text AST from the attempt snapshot."
- In "Authoring", add: "POST /api/authoring/preview returns the learner view plus a separate list of keys and explanations for authors (400 with diagnostics when the source is invalid). Diagnostics include `guidance`."

- [ ] **Step 3: Learner guide, testing, roadmap and accessibility docs**

`docs/user-guide.md`, in "Learn and navigate":
- Change "A course provides four views" to "A course provides up to five views". Add a bullet after **Content map**: "- **Glossary:** every term the course defines. In lessons, select a dotted term to show its definition."
- Change the **Lessons** bullet to: "teaching blocks (text, examples, callouts, code, math, tables, worked examples, misconceptions, definitions and primary sources), grouped by module, with saved reading completion."
- Add a paragraph: "Some lessons include **Check your understanding** questions. Choose an answer and select **Check answer**; the result is announced and explained. These checks are practice only: they do not affect mastery or readiness, and you can try again. In the **Learning library**, filter courses by subject, level and language."

`docs/testing.md`:
- Add to the list of covered areas: "the Markdown and TeX compilers and their limits, language tags, schema 1 upcasting with unchanged lesson hashes, schema 2 validation, every block kind, inline-check grading and privacy, legacy attempts, the Studio preview".
- Replace the browser-journey paragraph with: "The browser journeys register a learner, complete a lesson, use all five formats, resume, submit, review and confirm My courses. They also add, archive and restore a course with keyboard tabs; read rich lessons signed out, including an inline check answered by keyboard, a lesson link that moves focus, and the glossary; filter the library; render math in an attempt; and validate and preview a pack in Studio. Each rich-content journey runs an axe scan for WCAG 2.0, 2.1 and 2.2 A/AA rules (`@axe-core/playwright`). Axe does not replace the manual assistive-technology checks in the verification plan."

`docs/roadmap.md`: in the "Near-term" line, remove "rendered authoring preview, ". Then add a sentence: "Next for content: figures and media with reviewed alternatives, an asset store and a publishing gate bound to accessibility review records."

`docs/superpowers/specs/2026-09-25-learner-roadmap.md`:
- In the decisions table, change the Sequencing row to `Foundation first: SP1 → SP2a → SP2b ∥ SP6 → SP3 → SP4 → SP5 → SP7.`
- Replace the `### SP2 Rich, safe content` heading and bullets with:

```markdown
### SP2a Rich text and structure
Restricted Markdown and TeX compiled to a typed AST; math, table, worked example, misconception, definition, primary source and inline check blocks; stable block IDs, modules, glossary, language and direction, catalog metadata; duplicate JSON properties rejected. See the [design](2026-09-26-rich-content-design.md).

### SP2b Assets and media
- Figure (reviewed alternatives with explicit decorative handling) and media (required alternatives by media type, including captions and AA audio description) blocks. Follow the [content accessibility contract](../../accessibility/content-and-authoring.md).
- Pack-scoped assets with a SHA-256 manifest, MIME allowlist and a storage abstraction.
- The publishing gate bound to accessibility review records.
```

`docs/accessibility/content-and-authoring.md`:
- Replace the status line with: `Status: required design. Schema 2 (SP2a) implements language, structure, math and table contracts with machine checks; figures, media, assets and the review-record gate are **planned** (SP2b). Parent: [baseline](README.md).`
- In the second paragraph of "Content is part of conformance", replace its last two sentences with: "Schema 2 packs accept the rich text, math and table contracts below. Do not insert unrecognized properties: the compiler rejects unknown fields."
- In the "Compiler and publishing gate" table intro, replace "The following diagnostics are proposed additions to `ContentEngine`, not checks currently implemented." with "`A11Y_LANGUAGE`, `A11Y_STRUCTURE` and `A11Y_ALTERNATIVE` (math and tables) are implemented in `ContentEngine` for schema 2. `A11Y_MEDIA`, `A11Y_ASSET`, `A11Y_INTERACTION` and figure alternatives remain proposed."

- [ ] **Step 4: CLAUDE.md**

- In "Architecture", change the first sentence of the Core bullet to: "**`src/LearnForge.Core`**: subject-neutral, dependency-light rules (Markdig is its only package). `ContentEngine` compiles schema 1 sources (literal text, via `V1Upcaster`) and schema 2 sources (restricted Markdown and TeX, via `PackCompiler`, `RichTextCompiler` and `TexParser`) into one release model whose rich fields are a typed AST; `ReleaseReader` reads stored releases of either format."
- In the web bullet, add: "Rich content renders through `src/app/rich/*` (`lf-inlines`, `lf-blocks`, `lf-math`, `lf-lesson-block`, `lf-inline-check`), with `RichContext` provided per page."
- Add to "Invariants to preserve":

```markdown
- **Rich content is data.** Core compiles Markdown and TeX into a typed AST; nothing produces or injects HTML, and the web never uses `innerHTML` or `bypassSecurityTrust*`.
- **Inline-check keys live only in `Pack.Checks`.** Lessons are always delivery-safe; the check endpoint grades on the server and records no evidence.
- **Legacy content is read, never rewritten.** Schema 1 sources, releases without `format` and snapshots without `contentSchema` go through `Legacy/V1*` records and `V1Upcaster`. Never change the `V1*` records: stored data and lesson hashes depend on their shape.
```

- In "Conventions", replace "Content is rendered as text. Templates must not execute code or inject HTML." with: "Content is rendered as data: templates must not execute code or inject HTML. Keep rich-text Angular templates whitespace-exact."

- [ ] **Step 5: Final verification**

```bash
dotnet build LearnForge.slnx -c Release
make check-content
dotnet test LearnForge.slnx
docker compose up -d db
LEARNFORGE_TEST_POSTGRES='Host=127.0.0.1;Port=55432;Database=learnforge_test;Username=learnforge;Password=learnforge-local-only' dotnet test LearnForge.slnx --no-restore
make check-api-types
npm run build --prefix apps/web && npm test --prefix apps/web
```

Then start the app with `make dev` and run `cd apps/web && npx playwright test`.

Expected: every command succeeds, with no warnings in the Release build and no diff from `check-api-types`. Then do the manual checks at http://127.0.0.1:4300:
- the v2 lesson renders math, tables, terms and a primary source;
- an inline check works signed out;
- a v1 attempt made before the upgrade (on a copy of an existing development database) still shows its text;
- Studio preview works for a pack with a deliberate error and then a fixed one;
- the library filters work at 320 px width.

Record any manual screen-reader observations (VoiceOver; NVDA with MathCAT) as release evidence to capture, not as passes.

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md docs
git commit -m "docs: document schema 2 rich content, inline checks and the SP2 split

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
