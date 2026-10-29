import { useState, type ReactNode } from 'react';
import { ArrowRight, Check, Copy, Sparkles } from 'lucide-react';

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
  | { type: 'suggestion'; text: string }
  | { type: 'paragraph'; text: string };

export interface AgentMessageContentProps {
  text: string;
  isEn?: boolean;
  onPromptClick?: (prompt: string) => void;
}

export function AgentMessageContent({ text, isEn = false, onPromptClick }: AgentMessageContentProps) {
  const blocks = parseMessageBlocks(text);

  return (
    <div className="space-y-3 text-sm text-slate-200 leading-relaxed">
      {blocks.map((block, index) => {
        switch (block.type) {
          case 'code':
            return <CodeBlock key={index} code={block.code} language={block.language} isEn={isEn} />;

          case 'heading': {
            if (block.level === 1) {
              return (
                <h3 key={index} className="pt-2 pb-1 text-base font-bold text-white border-b border-white/10">
                  {formatInline(block.text)}
                </h3>
              );
            }
            return (
              <h4 key={index} className="pt-1.5 font-bold text-sm text-white">
                {formatInline(block.text)}
              </h4>
            );
          }

          case 'lead':
            return (
              <p key={index} className="font-semibold text-white text-sm mt-1">
                {formatInline(block.text)}
              </p>
            );

          case 'list': {
            const ListTag = block.isOrdered ? 'ol' : 'ul';
            return (
              <ListTag key={index} className="my-2.5 space-y-2.5">
                {block.items.map((item, itemIdx) => (
                  <li
                    key={itemIdx}
                    className="flex items-start gap-2.5 rounded-xl border border-white/10 bg-white/[0.025] p-3 hover:bg-white/[0.05] transition-colors shadow-xs"
                  >
                    {item.isOrdered ? (
                      <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-teal-500/20 text-[11px] font-bold text-teal-300">
                        {item.number}
                      </span>
                    ) : (
                      <span className="h-2 w-2 rounded-full bg-teal-400 mt-1.5 shrink-0 ring-4 ring-teal-400/20" />
                    )}
                    <div className="flex-1 text-slate-300 leading-relaxed text-sm">
                      {item.title && (
                        <span className="font-semibold text-teal-200 block sm:inline sm:mr-1.5">
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

          case 'suggestion':
            return (
              <div
                key={index}
                className="mt-3.5 rounded-2xl border border-teal-500/30 bg-gradient-to-r from-teal-500/15 via-slate-950 to-emerald-500/15 p-3.5 shadow-md space-y-2"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-teal-300">
                  <Sparkles className="h-3.5 w-3.5 text-teal-400" />
                  <span>{isEn ? 'Suggested Next Step' : 'Siguiente paso sugerido'}</span>
                </div>
                <p className="text-teal-100 font-medium leading-relaxed text-sm">
                  {formatInline(block.text)}
                </p>
                {onPromptClick && (
                  <button
                    type="button"
                    onClick={() => onPromptClick(block.text)}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-teal-400/30 bg-teal-500/20 hover:bg-teal-500/30 px-2.5 py-1 text-[11px] font-semibold text-teal-200 hover:text-white transition-colors cursor-pointer"
                  >
                    <span>{isEn ? 'Send' : 'Enviar'}</span>
                    <ArrowRight className="h-3 w-3" />
                  </button>
                )}
              </div>
            );

          case 'paragraph':
          default:
            return (
              <p key={index} className="text-slate-200 leading-relaxed text-sm">
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
    <div className="my-3 overflow-hidden rounded-xl border border-teal-500/30 bg-slate-950 text-slate-200 shadow-inner">
      <div className="flex items-center justify-between border-b border-teal-500/20 bg-slate-900/90 px-3.5 py-1.5 text-[11px] font-mono text-teal-300">
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
      <pre className="overflow-x-auto p-3.5 font-mono text-[11px] text-teal-200 leading-relaxed">
        <code>{code}</code>
      </pre>
    </div>
  );
}

function parseMessageBlocks(raw: string): Block[] {
  const blocks: Block[] = [];
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

          const colonIdx = rawBody.indexOf(':');
          const hasTitle = colonIdx > 0 && colonIdx < 45 && !rawBody.startsWith('http');
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

        flushList();

        const isLead =
          line.length < 80 &&
          line.endsWith(':') &&
          !/^(https?|ftp):/i.test(line);

        if (isLead) {
          blocks.push({
            type: 'lead',
            text: line,
          });
          continue;
        }

        const isEndingQuestion =
          line.endsWith('?') ||
          /^(would you like|do you want|shall we|¿te gustaría|deseas|quieres)/i.test(line);

        if (isEndingQuestion && lines.length === 1) {
          blocks.push({
            type: 'suggestion',
            text: line,
          });
          continue;
        }

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

function formatInline(text: string): ReactNode {
  const codeParts = text.split(/(`[^`]+`)/g);

  return codeParts.map((part, i) => {
    if (part.startsWith('`') && part.endsWith('`') && part.length >= 2) {
      return (
        <code
          key={i}
          className="rounded bg-teal-950/80 border border-teal-500/30 px-1.5 py-0.5 text-teal-200 font-mono text-[11px]"
        >
          {part.slice(1, -1)}
        </code>
      );
    }

    const boldParts = part.split(/(\*\*[^*]+\*\*)/g);
    return boldParts.map((bPart, j) => {
      if (bPart.startsWith('**') && bPart.endsWith('**') && bPart.length >= 4) {
        return (
          <strong key={`${i}-${j}`} className="font-semibold text-white">
            {formatSubInline(bPart.slice(2, -2), `${i}-${j}`)}
          </strong>
        );
      }

      return formatSubInline(bPart, `${i}-${j}`);
    });
  });
}

function formatSubInline(text: string, keyPrefix: string): ReactNode {
  if (text.includes(' → ') || text.includes(' -> ')) {
    const arrowTokens = text.split(/\s*(?:→|->)\s*/);
    return arrowTokens.map((tok, k) => (
      <span key={`${keyPrefix}-${k}`}>
        {formatLinks(tok, `${keyPrefix}-${k}`)}
        {k < arrowTokens.length - 1 && (
          <span className="inline-flex items-center justify-center mx-1.5 px-1.5 py-0.5 text-[10px] rounded-md bg-teal-500/20 text-teal-300 font-mono border border-teal-500/30 shadow-xs">
            →
          </span>
        )}
      </span>
    ));
  }

  return formatLinks(text, keyPrefix);
}

function formatLinks(text: string, keyPrefix: string): ReactNode {
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
        className="font-medium text-teal-300 underline hover:text-teal-200"
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
