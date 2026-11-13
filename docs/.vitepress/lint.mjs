// Checks the docs for the rules in docs/contributing/writing.md that a machine can verify. `npm run docs:check`
// runs it after markdownlint and before the build, which catches dead page links but not dead anchors.
//
//   - frontmatter: a title and a one-sentence description on every page but the home page
//   - one H1, the first heading, the same as the title; a `## Related` section last, which names pages by their titles
//   - every page in the sidebar; every sidebar and nav link a page and, with a #fragment, a heading or anchor on it
//   - every fence has a language; C# only between the markers mdsnippets writes, in README.md and PACKAGE.md too
//   - no page has a name mdsnippets skips, and no `snippet:` line stands as the tool should have found it
//   - every callout has a title
//   - every link into the docs reaches a page and, with a #fragment, a heading or anchor on it: from a page, from the
//     home page's cards, and by its address on the site; a link from README.md or PACKAGE.md, or to a file on GitHub,
//     reaches a file of the repository
//   - the guide and the home page hold no em dash, trailing "say", filler word, decision id or "plain C#", and the
//     two readmes no "plain C#"; the tutorial has at most five numbered steps
//   - in AGENTS.md, the playbooks and the contributing pages: every repository path in backticks exists, and every
//     `page.md#anchor` reaches a heading or anchor of that page
//   - every test docs/contributing/status.md names is a test under tests/

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { basename, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parse as parseYaml } from 'yaml'

const docs = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const root = resolve(docs, '..')
const skipped = new Set(['.vitepress', 'node_modules', 'templates', 'public'])
const callouts = ['tip', 'info', 'warning', 'danger', 'details']
const csharp = new Set(['cs', 'csharp', 'c#'])
const site = 'https://weboost-at.github.io/Typeshift/'
const repository = 'https://github.com/weboost-at/Typeshift'

// GitHub and NuGet show these two, the site does not: they are no pages, and GitHub's rules decide where a link leads
const readmes = [join(root, 'README.md'), join(root, 'src/Typeshift/PACKAGE.md')]

// mdsnippets 28 leaves a file of these names alone, in any folder and in any case
const skippedByMdsnippets = /^(agents|claude|gemini|copilot-instructions)\.md$/i

// Built from parts, so mdsnippets never takes this file's text for its own markers
const snippetStart = new RegExp('^<!-- ' + 'snippet: ')
const snippetEnd = new RegExp('^<!-- ' + 'endSnippet -->')
const unexpanded = new RegExp('^\\s*' + 'snippet:\\s*\\S+\\s*$')

const errors = []
const fail = (file, line, message) => errors.push(`${relative(root, file)}${line ? `:${line}` : ''}: ${message}`)

const walk = dir =>
  readdirSync(dir).flatMap(name => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return skipped.has(name) ? [] : walk(path)
    return name.endsWith('.md') ? [path] : []
  })

const routeOf = file => {
  const path = relative(docs, file).replace(/\\/g, '/').replace(/\.md$/, '')
  return path === 'index' ? '/' : '/' + path.replace(/(^|\/)index$/, '$1')
}

// VitePress's slugify (@mdit-vue/shared), applied to a heading's text without its Markdown
const slugify = text =>
  text
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[\u0000-\u001f]/g, '')
    .replace(/[\s~`!@#$%^&*()\-_+=[\]{}|\\;:"'“”‘’<>,.?/]+/g, '-')
    .replace(/-{2,}/g, '-')
    .replace(/^-+|-+$/g, '')
    .replace(/^(\d)/, '_$1')
    .toLowerCase()

const headingText = raw =>
  raw
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/<[^>]+>/g, '')
    .replace(/[`*]/g, '')
    .trim()

// GitHub's slug of a heading: lower case, punctuation dropped, a hyphen for each space
const githubSlug = text => text.toLowerCase().replace(/[^\p{L}\p{M}\p{N}\p{Pc}\- ]/gu, '').replace(/ /g, '-')

// A checkout may hold CRLF line endings, the rules read LF
const textOf = file => readFileSync(file, 'utf8').replace(/\r\n/g, '\n')

// Splits a page into its frontmatter and lines, marking which lines are inside a fence
const read = file => {
  const source = textOf(file)
  const match = source.match(/^---\n([\s\S]*?)\n---\n/)
  let frontmatter = null
  try {
    frontmatter = match ? (parseYaml(match[1]) ?? {}) : null
  } catch (error) {
    frontmatter = { unreadable: error.message.split('\n')[0] }
  }
  const offset = match ? match[0].split('\n').length - 1 : 0
  const lines = source.slice(match ? match[0].length : 0).split('\n')
  const fenced = []
  const opens = new Set()
  let fence = null

  lines.forEach((line, index) => {
    const marker = line.match(/^\s*(`{3,}|~{3,})/)
    fenced.push(Boolean(fence || marker))
    if (!fence && marker) {
      fence = marker[1]
      opens.add(index)
    } else if (fence && marker && marker[1][0] === fence[0] && marker[1].length >= fence.length
      && !line.trim().slice(marker[1].length))
      fence = null
  })

  return { frontmatter, lines, fenced, opens, offset }
}

const source = file => textOf(file).split('\n---\n')[0]

// A file as read() splits it, and which of its lines mdsnippets wrote
const parse = file => {
  const page = read(file)
  let inside = false

  page.generated = page.lines.map(line => {
    inside ||= snippetStart.test(line)
    const generated = inside
    if (snippetEnd.test(line)) inside = false
    return generated
  })

  return page
}

const pages = walk(docs)
const parsed = new Map([...pages, ...readmes].map(file => [file, parse(file)]))

const anchorsOf = ({ lines, fenced }, slug = slugify) => {
  const anchors = new Set()
  const seen = new Map()

  lines.forEach((line, index) => {
    for (const html of line.matchAll(/\bid=['"]([^'"]+)['"]/g)) anchors.add(html[1])
    const heading = !fenced[index] && line.match(/^#{1,6} (.+?)\s*$/)
    if (!heading) return

    // Only VitePress reads a heading's {#id}
    const custom = slug === slugify && heading[1].match(/\{#([^}]+)\}$/)
    if (custom) return anchors.add(custom[1])
    const name = slug(headingText(heading[1]))
    const count = seen.get(name) ?? 0
    seen.set(name, count + 1)
    anchors.add(count ? `${name}-${count}` : name)
  })

  return anchors
}

const anchors = new Map(pages.map(file => [file, anchorsOf(parsed.get(file))]))
const onSite = file => anchors.get(file)

const shown = new Map()
const onGitHub = file =>
  shown.get(file) ?? shown.set(file, anchorsOf(parsed.get(file) ?? read(file), githubSlug)).get(file)

const hosted = new RegExp(`^${repository}/(?:blob|tree)/main/([^?]+)`)

// Where a link leads and whose anchors its reader finds there: a page as the site serves it, a file as GitHub shows
// it, or nothing of this repository
const follow = (file, href) => {
  const [address, fragment] = href.split('#')
  const leaves = /^[a-z][a-z\d+.-]*:/i.test(address)
  const onMain = address === repository ? 'README.md' : address.match(hosted)?.[1]

  if (onMain || (!leaves && readmes.includes(file))) {
    const path = onMain ? join(root, decodeURI(onMain)) : address ? resolve(dirname(file), address) : file
    return { path: existsSync(path) ? path : null, fragment: path.endsWith('.md') ? fragment : null, anchors: onGitHub }
  }

  if (leaves && !address.startsWith(site)) return null
  const path = leaves ? `/${address.slice(site.length)}` : address

  // What the build writes beside the pages, llms.txt for example, is in no checkout
  if (leaves && /\.(?!md$)\w+$/.test(path)) return null
  if (!path) return { path: file, fragment, anchors: onSite }

  const absolute = path.startsWith('/') ? join(docs, path) : resolve(dirname(file), path)
  const candidates = path.endsWith('/')
    ? [join(absolute, 'index.md')]
    : absolute.endsWith('.md')
      ? [absolute]
      : [`${absolute}.md`, join(absolute, 'index.md')]

  return { path: candidates.find(existsSync) ?? null, fragment, anchors: onSite }
}

const checkLink = (file, line, href) => {
  const found = follow(file, href)
  if (!found) return

  if (!found.path)
    fail(file, line, `link "${href}" reaches no ${found.anchors === onSite ? 'page' : 'file of the repository'}`)
  else if (found.fragment && !found.anchors(found.path)?.has(found.fragment))
    fail(file, line, `link "${href}" reaches no heading or anchor on ${relative(root, found.path)}`)
}

const checkLinks = file => {
  const { lines, fenced, offset } = parsed.get(file)

  lines.forEach((line, index) => {
    if (fenced[index]) return

    for (const link of line.replace(/`[^`]*`/g, '').matchAll(/\]\(([^)\s]+)\)/g))
      checkLink(file, index + offset + 1, link[1])
  })
}

// Fences, and the lines mdsnippets was to replace
const checkCode = file => {
  const { lines, fenced, opens, generated, offset } = parsed.get(file)

  lines.forEach((line, index) => {
    if (!fenced[index] && unexpanded.test(line))
      fail(file, index + offset + 1, 'mdsnippets left this line as it is: the tool skips the file, or it did not run')

    if (!opens.has(index)) return

    const language = line.trim().replace(/^(`{3,}|~{3,})/, '').replace(/[\s{:].*$/, '').toLowerCase()
    if (!language) fail(file, index + offset + 1, 'a code block needs a language: text, bash, xml, json, mermaid, ...')
    if (csharp.has(language) && !generated[index])
      fail(file, index + offset + 1, 'C# comes from the samples through mdsnippets, never from a hand-written block')
  })
}

// What writing.md bans from the pages a user reads, each spelled as narrowly as its rule, so none needs an exemption
const months = '(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*'
const plainCSharp = [
  /\bplain\s+C#/gi,
  () => '"plain C#" reads as classes without a base class, and toolsets and agents have one'
]
const filler = match => `"${match[0].replace(/\s+/g, ' ')}" is filler: the sentence says the same without it`
const banned = [
  [/\u2014/g, () => 'an em dash: join the parts with a comma or a colon, or write two sentences'],
  [/,\s+say\s*[,;.:!?)]/gi, () => 'a trailing "say": an example says so with "for example" or "such as"'],
  plainCSharp,
  [/\b(?:basically|simply|it'?s\s+worth\s+noting|seamless(?:ly)?|robust(?:ly|ness)?)\b/gi, filler],
  // "the note that replaces it" is a noun: the filler opens a sentence, or follows one of these words
  [/\bNote\s+that\b|\b(?:[Pp]lease|[Aa]lso|to|should|must)\s+note\s+that\b/g, filler],
  [
    /\bD\d{1,2}\b/g,
    match => `${match[0]} is a decision id: give the reason in plain words, the ids stay on the contributing pages`
  ],
  [
    new RegExp(
      `\\b(?:updated|added|changed)\\s+on\\s+(?:\\d{4}\\b[\\d./-]*|\\d{1,2}[./-]\\d[\\d./-]*|${months}\\s+\\d+)`
        + '|\\b(?:new|added|changed|introduced)\\s+(?:in|since)\\s+(?:version\\s+|v)?\\d+\\.\\d+(?:[.-]\\w+)*',
      'gi'),
    match => `"${match[0].replace(/\s+/g, ' ')}" dates the page: the git history and the changelog say when`
  ]
]

// A file's prose, line for line. A fence, a generated snippet and a blank line become a mark no pattern crosses; code
// spans and link addresses are dropped, except a span that holds a decision id and nothing else
const proseOf = ({ lines, fenced, generated }) =>
  lines
    .map((line, index) =>
      fenced[index] || generated[index] || !line.trim()
        ? '\u0000'
        : line
          .replace(/`[^`]*`/g, span => (/^`D\d{1,2}`$/.test(span) ? span.slice(1, -1) : ''))
          .replace(/\]\([^)\s]*\)/g, ']'))
    .join('\n')

const strings = value =>
  typeof value === 'string' ? [value] : value && typeof value === 'object' ? Object.values(value).flatMap(strings) : []

const checkWords = (file, rules) => {
  const page = parsed.get(file)
  const prose = proseOf(page)

  for (const [pattern, message] of rules) {
    for (const match of prose.matchAll(pattern))
      fail(file, prose.slice(0, match.index).split('\n').length + page.offset, message(match))

    for (const text of strings(page.frontmatter))
      for (const match of text.matchAll(pattern)) fail(file, 0, `frontmatter: ${message(match)}`)
  }
}

for (const file of readmes) {
  checkCode(file)
  checkLinks(file)
  checkWords(file, [plainCSharp])
}

const tutorial = join(docs, 'guide/getting-started.md')
const linksIn = value =>
  Object.entries(value && typeof value === 'object' ? value : {})
    .flatMap(([key, item]) => (key === 'link' && typeof item === 'string' ? item : linksIn(item)))

for (const file of pages) {
  const { lines, fenced, offset } = parsed.get(file)
  const frontmatter = parsed.get(file).frontmatter ?? {}
  const home = frontmatter.layout === 'home'
  const at = index => index + offset + 1

  if (skippedByMdsnippets.test(basename(file)))
    fail(file, 0, `mdsnippets skips every file named ${basename(file).toLowerCase()}; rename the page`)

  checkCode(file)
  checkLinks(file)
  if (home || dirname(file) === join(docs, 'guide')) checkWords(file, banned)

  // The home page's cards and buttons are no Markdown, so the build's dead-link check never sees them
  for (const href of home ? linksIn(frontmatter) : []) checkLink(file, 0, href)

  if (home) continue

  if (frontmatter.unreadable) fail(file, 0, `frontmatter: ${frontmatter.unreadable}`)

  // VitePress's YAML parser rejects a plain value with ": " in it, where the linter's parser lets it pass
  for (const [key, value] of Object.entries(frontmatter))
    if (typeof value === 'string' && value.includes(': ') && new RegExp(`^${key}: [^"']`, 'm').test(source(file)))
      fail(file, 0, `frontmatter: quote the \`${key}\`, VitePress cannot read ": " in a plain value`)

  if (typeof frontmatter.title !== 'string' || !frontmatter.title.trim()) fail(file, 0, 'frontmatter: `title` is required')
  if (typeof frontmatter.description !== 'string' || !frontmatter.description.trim())
    fail(file, 0, 'frontmatter: `description` is required')
  else if (frontmatter.description.length > 200)
    fail(file, 0, 'frontmatter: `description` is one sentence of at most 200 characters')

  // Headings
  const headings = lines
    .map((line, index) => ({ match: !fenced[index] && line.match(/^(#{1,6}) (.+?)\s*$/), index }))
    .filter(heading => heading.match)
  const h1 = headings.filter(heading => heading.match[1] === '#')

  if (h1.length !== 1) fail(file, 0, `expected one H1, found ${h1.length}`)
  else if (headings[0] !== h1[0]) fail(file, at(h1[0].index), 'the H1 comes before any other heading')
  else if (headingText(h1[0].match[2]) !== frontmatter.title)
    fail(file, at(h1[0].index), `the H1 is "${headingText(h1[0].match[2])}", the title "${frontmatter.title}"`)

  const h2 = headings.filter(heading => heading.match[1] === '##')
  if (h2.at(-1)?.match[2] !== 'Related') fail(file, 0, 'the last section is `## Related`, a short list of links')

  const steps = file === tutorial ? h2.filter(heading => /^\d+\. /.test(heading.match[2])) : []
  if (steps.length > 5)
    fail(file, at(steps[5].index), `Getting started stays at about four steps, and this page has ${steps.length}`)

  let related = false

  lines.forEach((line, index) => {
    if (fenced[index]) return

    const callout = line.match(/^:::\s*(\w+)\s*(.*)$/)
    if (callout && callouts.includes(callout[1]) && !callout[2].trim())
      fail(file, at(index), `::: ${callout[1]} needs a title that tells the reader whether to stop`)

    // An entry that sends the reader to a whole page calls it by its title, so they know where they landed
    if (/^## /.test(line)) related = line.trim() === '## Related'
    const entry = related && line.match(/^- \[([^\]]+)\]\(([^)\s#]+)\)/)
    const title = entry && parsed.get(follow(file, entry[2])?.path)?.frontmatter?.title

    if (title && headingText(entry[1]) !== title)
      fail(file, at(index), `Related names a page by its title: "${title}", not "${entry[1]}"`)
  })
}

// Sidebar coverage, and where the sidebar and the nav lead
const config = readFileSync(join(docs, '.vitepress/config.mts'), 'utf8')
const menu = new Set([...config.matchAll(/link: '(\/[^']*)'/g)].map(match => match[1]))
const sidebar = new Set([...menu].map(link => link.split('#')[0]))
const routes = new Map(pages.map(file => [routeOf(file), file]))

for (const [route, file] of routes)
  if (route !== '/' && !sidebar.has(route)) fail(file, 0, `not in the sidebar: add { text, link: '${route}' }`)

for (const link of menu) {
  const [route, fragment] = link.split('#')

  if (!routes.has(route)) errors.push(`docs/.vitepress/config.mts: the link '${link}' has no page`)
  else if (fragment && !anchors.get(routes.get(route)).has(fragment))
    errors.push(`docs/.vitepress/config.mts: the link '${link}' reaches no heading or anchor on its page`)
}

// AGENTS.md, the playbooks and the contributing pages name paths, anchors and tests that move with the code. ci.yml
// skips a change that touches only docs/ or the root's Markdown, so a repository test would miss the edits that break
// them; the docs workflow runs this file on every pull request
const built = new Set(['bin', 'obj', 'node_modules', '.context'])

const find = (dir, wanted) =>
  readdirSync(dir).flatMap(name => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return built.has(name) ? [] : find(path, wanted)
    return wanted(name) ? [path] : []
  })

const playbooks = find(join(root, '.claude/skills'), name => name === 'SKILL.md')
const contributing = new Map([
  ...[join(root, 'AGENTS.md'), ...playbooks].map(file => [file, read(file)]),
  ...pages.filter(file => dirname(file) === join(docs, 'contributing')).map(file => [file, parsed.get(file)])
])

// The inline code of a file outside its fences, each span with its line
const spans = ({ lines, fenced, offset }) =>
  lines.flatMap((line, index) =>
    fenced[index] ? [] : [...line.matchAll(/`([^`]+)`/g)].map(match => ({ text: match[1], line: index + offset + 1 })))

const projects = ['src', 'tests', 'samples', 'demos']
  .flatMap(folder => find(join(root, folder), name => name.endsWith('.csproj')))

const frameworks = new Set(
  projects
    .flatMap(project => [...readFileSync(project, 'utf8').matchAll(/<TargetFrameworks?>([^<]+)</g)])
    .flatMap(match => match[1].split(';')))

// A path is written from the repository's root, from docs/ or the guide, or from a project's folder; a test class
// goes by its file's name without the extension, with or without the test it names after a dot
const bases = [root, docs, join(docs, 'guide'), ...projects.map(dirname)]
const isFolder = path => existsSync(path) && statSync(path).isDirectory()
const isInRepository = path =>
  [path, `${path}.cs`, `${path.replace(/\.\w+$/, '')}.cs`].some(spelling => bases.some(base => existsSync(join(base, spelling))))

for (const [file, content] of contributing) {
  for (const { text, line } of spans(content)) {
    const [path, fragment] = text.split('#')
    const segments = path.replace(/\/$/, '').split('/')

    // Commands, placeholders, globs and the ./ links writing.md shows are no paths of the repository
    if (!/^[\w.-]+(\/[\w.-]+)*\/?$/.test(path) || segments[0] === '.' || segments[0] === '..') continue

    if (segments.length > 1) {
      const framework = segments.find(segment => /^net(standard|coreapp)?\d/.test(segment))
      const known = !built.has(segments[0]) && bases.some(base => isFolder(join(base, segments[0])))

      // What a build or a pack writes is in no checkout: of a path with a target framework only the framework can
      // be checked, and a snapshot's received file exists only until it is accepted
      if (framework)
        frameworks.has(framework)
          || fail(file, line, `\`${text}\` names the target framework ${framework}, which no project targets`)
      else if (known && !path.includes('.received.') && !isInRepository(path))
        fail(file, line, `\`${text}\` names no file or folder of the repository`)
    }

    if (!fragment || !path.endsWith('.md')) continue

    const page = [join(docs, path), join(docs, 'guide', path)].find(candidate => anchors.has(candidate))

    if (page && !anchors.get(page).has(fragment))
      fail(file, line, `\`${text}\` reaches no heading or anchor on ${relative(docs, page)}`)
    else if (!page && segments.length === 1 && !existsSync(join(root, path)))
      fail(file, line, `\`${text}\` names no page of the docs`)
  }
}

// status.md names a test as `Class.Method`, as `..._Method` for another test of the class before it, or by its class
const tests = new Map()

for (const file of find(join(root, 'tests'), name => name.endsWith('.cs'))) {
  const code = readFileSync(file, 'utf8')
  const methods = [...code.matchAll(/\bpublic\s+(?:async\s+)?[\w<>[\],.? ]+\s(\w+)\(/g)].map(match => match[1])

  for (const [, type] of code.matchAll(/\bclass\s+(\w+)/g))
    tests.set(type, new Set([...(tests.get(type) ?? []), ...methods]))
}

const status = join(docs, 'contributing/status.md')
const coverage = parsed.get(status) ?? { lines: [], fenced: [], offset: 0 }

coverage.lines.forEach((row, index) => {
  if (coverage.fenced[index] || !row.startsWith('|')) return
  let type = null

  for (const [, text] of row.matchAll(/`([^`]+)`/g)) {
    const named = text.match(/^(\w+Tests)(?:\.(\w+))?$/)
    const method = named ? named[2] : text.match(/^\.\.\._(\w+)$/)?.[1]
    if (named) type = named[1]
    if (!named && !method) continue

    if (!tests.has(type))
      fail(status, index + coverage.offset + 1, `\`${text}\` names no test class under tests/`)
    else if (method && !tests.get(type).has(method))
      fail(status, index + coverage.offset + 1, `\`${text}\` names no test: ${type} has no ${method}`)
  }
})

for (const error of errors) console.error(`  ${error}`)

if (errors.length) {
  console.error(`\n${errors.length} problem(s). The rules are in docs/contributing/writing.md.\n`)
  process.exit(1)
}

console.log(`${pages.length} pages follow the docs rules`)
