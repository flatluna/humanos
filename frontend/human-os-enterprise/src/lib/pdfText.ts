import * as pdfjsLib from 'pdfjs-dist';
// Vite-specific "?url" import gives us the built worker file's final URL so it
// can be loaded in a separate thread — required by pdfjs-dist, otherwise
// `getDocument()` throws "No GlobalWorkerOptions.workerSrc specified".
import pdfWorkerUrl from 'pdfjs-dist/build/pdf.worker.mjs?url';

pdfjsLib.GlobalWorkerOptions.workerSrc = pdfWorkerUrl;

/**
 * Extracts all plain text from a PDF file, entirely client-side (no backend
 * round-trip, no Data Lake storage dependency) — used by EmployeeDetailPage's
 * "Subir PDF" option so an HRAdmin can upload a résumé PDF directly instead
 * of copy/pasting its text before running "Extraer con IA".
 */
export async function extractPdfText(file: File): Promise<string> {
  const buffer = await file.arrayBuffer();
  const pdf = await pdfjsLib.getDocument({ data: buffer }).promise;

  const pageTexts: string[] = [];
  for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber++) {
    const page = await pdf.getPage(pageNumber);
    const content = await page.getTextContent();
    const text = content.items
      .map((item) => ('str' in item ? item.str : ''))
      .join(' ');
    pageTexts.push(text);
  }

  return pageTexts.join('\n\n').trim();
}
