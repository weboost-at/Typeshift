// Checks the docs for the rules in docs/contributing/writing.md that a machine can verify. `npm run docs:check`
// runs it after markdownlint and before the build, which catches dead page links but not dead anchors.
//
//   - frontmatter: a title and a one-sentence description on every page but the home page
//   - one H1, the first heading, the same as the title; a `## Related` section last
//   - every page in the sidebar, every sidebar link a page
//   - every fence has a language; C# only between the markers mdsnippets writes
//   - every callout has a title
//   - every link into the docs reaches a page and, with a #fragment, a heading or anchor on it

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { basename, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parse as parseYaml } from 'yaml'

const docs = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const root = resolve(docs, '..')
const skipped = new Set(['.vitepress', 'node_modules', 'templates', 'public'])
const callouts = ['tip', 'info', 'warning', 'danger', 'details']
const csharp = new Set(['cs', 'csharp', 'c#'])

// Built from parts, so mdsnippets never takes this file's text for its own markers
const snippetStart = new RegExp('^<!-- ' + 'snippet: ')
const snippetEnd = new RegExp('^<!-- ' + 'endSnippet -->')

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

// Splits a page into its frontmatter and lines, marking which lines are inside a fence
const read = file => {
  const source = readFileSync(file, 'utf8')
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

const source = file => readFileSync(file, 'utf8').split('\n---\n')[0]

const pages = walk(docs)
const parsed = new Map(pages.map(file => [file, read(file)]))

const anchorsOf = file => {
  const { lines, fenced } = parsed.get(file)
  const anchors = new Set()
  const seen = new Map()

  lines.forEach((line, index) => {
    for (const html of line.matchAll(/\bid=['"]([^'"]+)['"]/g)) anchors.add(html[1])
    const heading = !fenced[index] && line.match(/^#{1,6} (.+?)\s*$/)
    if (!heading) return
    const custom = heading[1].match(/\{#([^}]+)\}$/)
    if (custom) return anchors.add(custom[1])
    const slug = slugify(headingText(heading[1]))
    const count = seen.get(slug) ?? 0
    seen.set(slug, count + 1)
    anchors.add(count ? `${slug}-${count}` : slug)
  })

  return anchors
}

const anchors = new Map(pages.map(file => [file, anchorsOf(file)]))

const target = (file, href) => {
  const [path, fragment] = href.split('#')
  if (!path) return [file, fragment]
  const absolute = path.startsWith('/') ? join(docs, path) : resolve(dirname(file), path)
  const candidates = absolute.endsWith('/')
    ? [join(absolute, 'index.md')]
    : absolute.endsWith('.md')
      ? [absolute]
      : [`${absolute}.md`, join(absolute, 'index.md')]
  return [candidates.find(existsSync), fragment]
}

for (const file of pages) {
  const { lines, fenced, opens, offset } = parsed.get(file)
  const frontmatter = parsed.get(file).frontmatter ?? {}
  const home = frontmatter.layout === 'home'
  const at = index => index + offset + 1

  if (basename(file).toLowerCase() === 'agents.md') fail(file, 0, 'mdsnippets skips files named agents.md; rename it')

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

  // Fences and callouts
  let snippet = false
  lines.forEach((line, index) => {
    if (snippetStart.test(line)) snippet = true
    if (snippetEnd.test(line)) snippet = false

    if (opens.has(index)) {
      const language = line.trim().replace(/^(`{3,}|~{3,})/, '').replace(/[\s{:].*$/, '')
      if (!language) fail(file, at(index), 'a code block needs a language: text, bash, xml, json, mermaid, ...')
      if (csharp.has(language) && !snippet)
        fail(file, at(index), 'C# comes from the samples through mdsnippets, never from a hand-written block')
    }

    const callout = !fenced[index] && line.match(/^:::\s*(\w+)\s*(.*)$/)
    if (callout && callouts.includes(callout[1]) && !callout[2].trim())
      fail(file, at(index), `::: ${callout[1]} needs a title that tells the reader whether to stop`)
  })

  // Links into the docs
  lines.forEach((line, index) => {
    if (fenced[index]) return
    for (const link of line.replace(/`[^`]*`/g, '').matchAll(/\]\(([^)\s]+)\)/g)) {
      const href = link[1]
      if (/^[a-z]+:/i.test(href)) continue
      const [page, fragment] = target(file, href)
      if (!page) fail(file, at(index), `link "${href}" reaches no page`)
      else if (fragment && !anchors.get(page)?.has(fragment))
        fail(file, at(index), `link "${href}" reaches no heading or anchor on ${relative(docs, page)}`)
    }
  })
}

// Sidebar coverage
const config = readFileSync(join(docs, '.vitepress/config.mts'), 'utf8')
const sidebar = new Set([...config.matchAll(/link: '(\/[^'#]*)'/g)].map(match => match[1]))
const routes = new Map(pages.map(file => [routeOf(file), file]))

for (const [route, file] of routes)
  if (route !== '/' && !sidebar.has(route)) fail(file, 0, `not in the sidebar: add { text, link: '${route}' }`)

for (const link of sidebar)
  if (!routes.has(link)) errors.push(`docs/.vitepress/config.mts: the link '${link}' has no page`)

for (const error of errors) console.error(`  ${error}`)

if (errors.length) {
  console.error(`\n${errors.length} problem(s). The rules are in docs/contributing/writing.md.\n`)
  process.exit(1)
}

console.log(`${pages.length} pages follow the docs rules`)
