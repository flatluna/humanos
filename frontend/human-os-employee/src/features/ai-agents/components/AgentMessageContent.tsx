import { useState, type ReactNode } from 'react';
import { ArrowRight, Check, Copy, Info, Sparkles } from 'lucide-react';

interface ListItem {
  isOrdered: boolean;
  number?: string;
  title?: string;
  body: string;
}

type Block =
  | { type: 'code'; code: string; language?: string }
  | { type: 'heading'; text: string; level: number }
  | { type: 'lead'; text: string }
  | { type: 'list'; isOrdered: boolean; items: ListItem[] }
  | { type: 'callout'; text: string }
  | { type: 'suggestion'; text: string }
  | { type: 'paragraph'; text: string };

export interface AgentMessageContentProps {
  text: string;
  isEn?: boolean;
  onPromptClick?: (prompt: string) => void;
}

/**
 * Parses and renders rich structured output from AI Agents:
 * - Code blocks with copy button
 * - Headings (#, ##, ###)
 * - Lead-in subtitles ("reglamento:", "Puntos clave:")
 * - Unordered / ordered lists rendered as elevated interactive cards
 * - Callouts (amendment notes, "Además,", "Nota:", "Importante:")
 * - Suggested next steps / prompt suggestions
 * - Inline formatting (bold, code, links, arrows)
 */
export function AgentMessageContent({ text, isEn = false, onPromptClick }: AgentMessageContentProps) {
  const blocks = parseMessageBlocks(text);

  return (
    <div className="space-y-3 text-sm text-slate-800 dark:text-slate-200 leading-relaxed">
      {blocks.map((block, index) => {
        switch (block.type) {
          case 'code':
            return <CodeBlock key={index} code={block.code} language={block.language} isEn={isEn} />;

          case 'heading': {
            if (block.level === 1) {
              return (
                <h3 key={index} className="pt-2 pb-1 text-base font-bold text-slate-900 dark:text-white border-b border-slate-200 dark:border-white/10">
                  {formatInline(block.text)}
                </h3>
              );
            }
            return (
              <h4 key={index} className="pt-1.5 font-bold text-sm text-slate-900 dark:text-white">
                {formatInline(block.text)}
              </h4>
            );
          }

          case 'lead':
            return (
              <div key={index} className="mt-1 flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
                <span className="h-1.5 w-1.5 rounded-full bg-brand-500 dark:bg-cyan-400" />
                <span>{formatInline(block.text)}</span>
              </div>
            );

          case 'list': {
            const ListTag = block.isOrdered ? 'ol' : 'ul';
            return (
              <ListTag key={index} className="my-2.5 space-y-2">
                {block.items.map((item, itemIdx) => (
                  <li
                    key={itemIdx}
                    className="flex items-start gap-3 rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5 text-sm leading-relaxed text-slate-800 transition-colors hover:bg-slate-100/80 dark:border-white/10 dark:bg-white/[0.03] dark:text-slate-200 dark:hover:bg-white/[0.06] shadow-xs"
                  >
                    {item.isOrdered ? (
                      <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-lg bg-brand-500/10 text-[11px] font-bold text-brand-700 dark:bg-cyan-400/15 dark:text-cyan-300">
                        {item.number}
                      </span>
                    ) : (
                      <span className="mt-2 h-2 w-2 shrink-0 rounded-full bg-brand-500 ring-4 ring-brand-500/20 dark:bg-cyan-400 dark:ring-cyan-400/20" />
                    )}
                    <div className="flex-1 leading-relaxed">
                      {item.title && (
                        <span className="block sm:inline sm:mr-1.5 font-semibold text-brand-700 dark:text-cyan-300">
                          {formatInline(item.title)}
                        </span>
                      )}
                      {formatInline(item.body)}
                    </div>
                  </li>
                ))}
              </ListTag>
            );
          }

          case 'callout':
            return (
              <div
                key={index}
                className="my-3 flex items-start gap-3 rounded-xl border border-indigo-200/90 bg-indigo-50/70 p-3.5 text-xs sm:text-sm leading-relaxed text-indigo-950 dark:border-indigo-500/20 dark:bg-indigo-500/10 dark:text-indigo-200 shadow-xs"
              >
                <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-indigo-100 text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300">
                  <Info className="h-4 w-4" />
                </div>
                <div className="flex-1 font-medium leading-relaxed">
                  {formatInline(block.text)}
                </div>
              </div>
            );

          case 'suggestion':
            return (
              <div
                key={index}
                className="my-3 rounded-xl border border-teal-500/30 bg-gradient-to-r from-teal-50/80 via-white to-emerald-50/80 p-3.5 shadow-xs dark:border-teal-500/30 dark:from-teal-500/15 dark:via-slate-950 dark:to-emerald-500/15"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-teal-600 dark:text-teal-300">
                  <Sparkles className="h-3.5 w-3.5 text-teal-500 dark:text-teal-400" />
                  <span>{isEn ? 'Suggested Next Step' : 'Siguiente paso sugerido'}</span>
                </div>
                <p className="mt-1 text-sm font-medium leading-relaxed text-teal-950 dark:text-teal-100">
                  {formatInline(block.text)}
                </p>
                {onPromptClick && (
                  <button
                    type="button"
                    onClick={() => onPromptClick(block.text)}
                    className="mt-2.5 inline-flex items-center gap-1.5 rounded-lg border border-teal-400/30 bg-teal-500/10 px-2.5 py-1 text-xs font-semibold text-teal-700 hover:bg-teal-500/20 dark:border-teal-400/30 dark:bg-teal-500/20 dark:text-teal-200 dark:hover:bg-teal-500/30 dark:hover:text-white transition cursor-pointer"
                  >
                    <span>{isEn ? 'Send this' : 'Enviar esto'}</span>
                    <ArrowRight className="h-3 w-3" />
                  </button>
                )}
              </div>
            );

          case 'paragraph':
          default:
            return (
              <p key={index} className="text-sm leading-relaxed text-slate-700 dark:text-slate-200">
                {formatInline(block.text)}
              </p>
            );
        }
      })}
    </div>
  );
}

function CodeBlock({ code, language, isEn }: { code: string; language?: string; isEn: boolean }) {
  const [copied, setCopied] = useState(false);

  function handleCopy() {
    void navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="my-3 overflow-hidden rounded-xl border border-slate-800 bg-slate-950 text-slate-200 shadow-md">
      <div className="flex items-center justify-between border-b border-slate-800 bg-slate-900/80 px-4 py-1.5 text-[11px] font-mono text-slate-400">
        <span className="uppercase">{language || 'code'}</span>
        <button
          type="button"
          onClick={handleCopy}
          className="inline-flex items-center gap-1 text-slate-400 hover:text-white transition cursor-pointer"
          title={isEn ? 'Copy code' : 'Copiar código'}
        >
          {copied ? <Check className="h-3.5 w-3.5 text-emerald-400" /> : <Copy className="h-3.5 w-3.5" />}
          <span>{copied ? (isEn ? 'Copied' : 'Copiado') : (isEn ? 'Copy' : 'Copiar')}</span>
        </button>
      </div>
      <pre className="overflow-x-auto p-4 font-mono text-xs text-brand-200 dark:text-cyan-200 leading-relaxed">
        <code>{code}</code>
      </pre>
    </div>
  );
}

/** Parses markdown into distinct semantic blocks. */
function parseMessageBlocks(raw: string): Block[] {
  const blocks: Block[] = [];
  // 1. Separate code fences
  const codeBlockSplit = raw.split(/(```[\s\S]*?```)/g);

  for (const chunk of codeBlockSplit) {
    if (!chunk.trim()) continue;

    if (chunk.startsWith('```') && chunk.endsWith('```')) {
      const inside = chunk.slice(3, -3);
      const lines = inside.split('\n');
      const firstLine = lines[0]?.trim() ?? '';
      const isLang = /^[a-z0-9_-]+$/i.test(firstLine);
      const language = isLang ? firstLine : undefined;
      const code = (isLang ? lines.slice(1) : lines).join('\n').trim();
      blocks.push({ type: 'code', code, language });
      continue;
    }

    // 2. Process text chunk paragraph by paragraph or section
    const paragraphs = chunk.split(/\n\s*\n/).map((p) => p.trim()).filter(Boolean);

    for (const para of paragraphs) {
      const lines = para.split('\n').map((l) => l.trim()).filter(Boolean);
      if (lines.length === 0) continue;

      let currentList: ListItem[] | null = null;
      let currentListOrdered = false;

      function flushList() {
        if (currentList && currentList.length > 0) {
          blocks.push({
            type: 'list',
            isOrdered: currentListOrdered,
            items: currentList,
          });
          currentList = null;
        }
      }

      for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        // Heading: #, ##, ###
        const headingMatch = line.match(/^(#{1,4})\s+(.+)$/);
        if (headingMatch) {
          flushList();
          blocks.push({
            type: 'heading',
            level: headingMatch[1].length,
            text: headingMatch[2],
          });
          continue;
        }

        // Callout check: starts with '>', or starts with keyword like 'Además,', 'Nota:', 'Importante:', 'Reformado:', etc.
        const isCallout =
          /^>\s+/.test(line) ||
          /^(?:(?:\*{1,2})?(?:Además|Nota|Importante|Aviso|Atención|DOF|Reformado|Tip|Important|Note)(?:\*{1,2})?\s*[:,\-—])\s*/i.test(line);

        if (isCallout) {
          flushList();
          const cleanText = line.replace(/^>\s*/, '');
          blocks.push({
            type: 'callout',
            text: cleanText,
          });
          continue;
        }

        // List item check: bullet or ordered
        const bulletMatch = line.match(/^([-*•])\s+(.+)$/);
        const orderedMatch = line.match(/^(\d+)[.)]\s+(.+)$/);

        if (bulletMatch || orderedMatch) {
          const isOrdered = Boolean(orderedMatch);
          const rawBody = bulletMatch ? bulletMatch[2] : orderedMatch![2];
          const number = orderedMatch ? orderedMatch[1] : undefined;

          if (!currentList || currentListOrdered !== isOrdered) {
            flushList();
            currentList = [];
            currentListOrdered = isOrdered;
          }

          // Extract title if present (e.g. "Title: Body" or "**Title:** Body")
          const colonIdx = rawBody.indexOf(':');
          const hasTitle = colonIdx > 0 && colonIdx < 45;
          const title = hasTitle ? rawBody.slice(0, colonIdx + 1).trim() : undefined;
          const body = hasTitle ? rawBody.slice(colonIdx + 1).trim() : rawBody;

          currentList.push({
            isOrdered,
            number,
            title,
            body,
          });
          continue;
        }

        // Not a list item
        flushList();

        // Lead-in label line (e.g. "reglamento:", "Puntos principales:")
        const isLead =
          line.length < 70 &&
          line.endsWith(':') &&
          !/^(https?|ftp):/i.test(line);

        if (isLead) {
          blocks.push({
            type: 'lead',
            text: line,
          });
          continue;
        }

        // Suggestion / ending question
        const isSuggestion =
          line.endsWith('?') &&
          (/^(¿|\b(?:would you like|do you want|shall we|¿te gustaría|te gustaría|deseas|quieres)\b)/i.test(line) ||
            lines.length === 1);

        if (isSuggestion && (blocks.length > 0 || lines.length === 1)) {
          blocks.push({
            type: 'suggestion',
            text: line,
          });
          continue;
        }

        // Regular paragraph line
        blocks.push({
          type: 'paragraph',
          text: line,
        });
      }

      flushList();
    }
  }

  return blocks;
}

/** Formats inline text: code spans, bold, arrows, links */
function formatInline(text: string): ReactNode {
  // 1. Backtick code spans
  const codeParts = text.split(/(`[^`]+`)/g);

  return codeParts.map((part, i) => {
    if (part.startsWith('`') && part.endsWith('`') && part.length >= 2) {
      return (
        <code
          key={i}
          className="rounded-md border border-slate-300/80 bg-slate-100 px-1.5 py-0.5 font-mono text-[12px] text-brand-700 dark:border-white/10 dark:bg-white/[0.08] dark:text-cyan-300"
        >
          {part.slice(1, -1)}
        </code>
      );
    }

    // 2. Bold (**text**)
    const boldParts = part.split(/(\*\*[^*]+\*\*)/g);
    return boldParts.map((bPart, j) => {
      if (bPart.startsWith('**') && bPart.endsWith('**') && bPart.length >= 4) {
        return (
          <strong key={`${i}-${j}`} className="font-semibold text-slate-900 dark:text-white">
            {formatSubInline(bPart.slice(2, -2), `${i}-${j}`)}
          </strong>
        );
      }

      return formatSubInline(bPart, `${i}-${j}`);
    });
  });
}

function formatSubInline(text: string, keyPrefix: string): ReactNode {
  // Arrow tokens
  if (text.includes(' → ') || text.includes(' -> ')) {
    const arrowTokens = text.split(/\s*(?:→|->)\s*/);
    return arrowTokens.map((tok, k) => (
      <span key={`${keyPrefix}-${k}`}>
        {formatLinks(tok, `${keyPrefix}-${k}`)}
        {k < arrowTokens.length - 1 && (
          <span className="mx-1.5 inline-flex items-center justify-center rounded-md border border-brand-300/40 bg-brand-500/10 px-1.5 py-0.5 font-mono text-[10px] text-brand-700 dark:border-cyan-400/30 dark:bg-cyan-400/20 dark:text-cyan-300">
            →
          </span>
        )}
      </span>
    ));
  }

  return formatLinks(text, keyPrefix);
}

function formatLinks(text: string, keyPrefix: string): ReactNode {
  // Markdown links: [label](url)
  const linkRegex = /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g;
  if (!linkRegex.test(text)) {
    return text;
  }

  const parts: ReactNode[] = [];
  let lastIndex = 0;
  linkRegex.lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = linkRegex.exec(text)) !== null) {
    if (match.index > lastIndex) {
      parts.push(text.slice(lastIndex, match.index));
    }
    const [, label, url] = match;
    parts.push(
      <a
        key={`${keyPrefix}-${match.index}`}
        href={url}
        target="_blank"
        rel="noopener noreferrer"
        className="font-medium text-brand-600 underline hover:text-brand-700 dark:text-cyan-400 dark:hover:text-cyan-300"
      >
        {label}
      </a>
    );
    lastIndex = linkRegex.lastIndex;
  }

  if (lastIndex < text.length) {
    parts.push(text.slice(lastIndex));
  }

  return parts;
}
