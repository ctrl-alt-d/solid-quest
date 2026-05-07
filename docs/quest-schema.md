# Quest YAML Schema

Solid Quest loads quests from **YAML files** hosted anywhere (GitHub raw, gist, your own CDN, or embedded fallback).

## Structure Overview

```yaml
title: string (required)
image: string (optional, URL)
image_alt: string (optional, accessibility description)

questions:
  - title: string (required, supports Markdown)
    image: string (optional, URL)
    image_alt: string (optional, accessibility description)
    options: array of 4 strings (required, supports Markdown)
    correct_answer: integer 1-4 (required, 1-indexed)
    explanation: string (optional, supports Markdown)
```

## Field Reference

### Quest Metadata

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | ✅ Yes | Quest title shown in preview and enrollment screen |
| `image` | string (URL) | ⬜ No | Cover image URL (shown during enrollment, before first question) |
| `image_alt` | string | ⬜ No | Alt text for cover image (accessibility) |

### Question Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | ✅ Yes | Question text (Markdown supported: bold, italic, code, links) |
| `image` | string (URL) | ⬜ No | Question illustration URL (displayed alongside question text) |
| `image_alt` | string | ⬜ No | Alt text for question image (accessibility) |
| `options` | array[4] of string | ✅ Yes | Exactly 4 answer options (Markdown supported) |
| `correct_answer` | integer (1-4) | ✅ Yes | Index of correct answer (1 = first option, 2 = second, etc.) |
| `explanation` | string | ⬜ No | Shown after question closes (Markdown supported, including code blocks) |

## Markdown Support

All text fields (`text`, `options`, `explanation`) support **GitHub-Flavored Markdown**:

- **Bold**: `**text**` or `__text__`
- *Italic*: `*text*` or `_text_`
- `Inline code`: `` `code` ``
- [Links](url): `[text](url)`
- Code blocks: triple backticks with language
- Lists: `- item` or `1. item`
- **Images in markdown are discouraged** — use `image` field instead for proper responsive handling

### Code Block Example

<pre>
explanation: |
  The correct answer is **recursion**.
  
  ```python
  def factorial(n):
      return 1 if n == 0 else n * factorial(n - 1)
  ```
  
  This is a classic recursive pattern.
</pre>

Renders with syntax highlighting in dark macOS-style code windows.

## Image Guidelines

### Responsive Images

- Use the `image` field for quest covers and question illustrations
- Avoid embedding images in Markdown text (they won't be responsive)
- Images scale with `object-fit: contain` to fit any screen size
- No scrolling required — layout adapts to viewport

### Accessibility

- **Always provide `image_alt`** when using `image` field
- Alt text should describe the content, not the file name
- Good: `image_alt: "Diagram showing TCP three-way handshake"`
- Bad: `image_alt: "image.png"`

### Image Hosting

- **GitHub raw URLs**: `https://raw.githubusercontent.com/user/repo/main/image.png`
- **Imgur**: Direct link (ends with `.jpg`, `.png`, etc.)
- **Your CDN**: Any public URL, CORS not required
- **Avoid**: Hotlink-protected sites, temporary upload services

## Validation Rules

### Required Fields
- Quest must have `title`
- Each question must have `title`, `options` (exactly 4), and `correct_answer`

### Constraints
- `correct_answer` must be 1, 2, 3, or 4 (1-indexed)
- `options` must have exactly 4 items (no more, no less)
- Empty strings are invalid for required fields

### Optional Fields
- `image` and `image_alt` can be omitted (null or missing)
- `explanation` can be omitted (no explanation shown)
- If `image` is provided, `image_alt` is strongly recommended but not enforced

## Example: Minimal Quest

```yaml
title: Quick Math Quiz

questions:
  - title: What is 2 + 2?
    options:
      - "3"
      - "4"
      - "5"
      - "22"
    correct_answer: 2
    explanation: Basic arithmetic. The answer is 4.
```

## Example: Full-Featured Quest

```yaml
title: Advanced TypeScript Patterns
image: https://example.com/typescript-logo.png
image_alt: TypeScript logo with blue background

questions:
  - title: |
      What does the `readonly` modifier do in TypeScript?
    image: https://example.com/readonly-example.png
    image_alt: Code snippet showing readonly array declaration
    options:
      - "Prevents variable reassignment"
      - "Prevents object property mutation"
      - "Makes the variable immutable at runtime"
      - "Only works with arrays"
    correct_answer: 2
    explanation: |
      The `readonly` modifier prevents **property mutation** at compile time.
      
      ```typescript
      interface User {
        readonly id: number;
        name: string;
      }
      
      const user: User = { id: 1, name: "Alice" };
      user.id = 2; // ❌ Error: Cannot assign to 'id'
      user.name = "Bob"; // ✅ OK
      ```
      
      It's a compile-time check only — no runtime enforcement.

  - title: Which utility type makes all properties optional?
    options:
      - "`Partial<T>`"
      - "`Required<T>`"
      - "`Readonly<T>`"
      - "`Pick<T, K>`"
    correct_answer: 1
    explanation: |
      **`Partial<T>`** makes all properties optional.
      
      ```typescript
      interface User {
        id: number;
        name: string;
      }
      
      type PartialUser = Partial<User>;
      // { id?: number; name?: string; }
      ```
```

## Hosting Your Quest

### GitHub Raw URL

1. Push YAML to your repo: `quests/my-quest.yaml`
2. Get raw URL: `https://raw.githubusercontent.com/user/repo/main/quests/my-quest.yaml`
3. Paste URL in Solid Quest settings card

### GitHub Gist

1. Create gist with `.yaml` extension
2. Click "Raw" button
3. Copy URL (e.g., `https://gist.githubusercontent.com/user/.../raw/.../file.yaml`)
4. Paste in Solid Quest

### Your Own Server

1. Host YAML anywhere public
2. Ensure content-type is `text/yaml` or `application/x-yaml` (optional but recommended)
3. **CORS not required** — Solid Quest fetches server-side

## Tips

- **Start small**: 4-6 questions for a 5-minute game
- **Test locally**: Use embedded sample first, then try remote URL
- **Markdown preview**: Use online tools to preview rendering before game time
- **Alt text matters**: Screen readers rely on it, don't skip
- **Image size**: 1920×1080 works well for projection mode, but smaller is fine (layout adapts)

## Sample Quests

- [Tech Memes Quest](../samples/quests/tech-memes.yaml) — internet culture + programming humor
- More coming soon...

---

**Ready to create?** Copy the minimal example, fill in your questions, host it, and paste the URL. 🎮
