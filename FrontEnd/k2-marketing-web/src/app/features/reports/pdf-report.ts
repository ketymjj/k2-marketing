import { AgencyBrand } from '../../shared/models/models';

export interface PdfSection {
  title?: string;
  kpis?: { label: string; value: string; sub?: string }[];
  table?: { head: string[]; body: (string | number)[][]; foot?: (string | number)[] };
  images?: { data: string; caption: string }[];
  bullets?: { text: string; tone?: 'positive' | 'negative' | 'neutral' }[];
  text?: string;
}

export interface PdfReport {
  title: string;
  subtitle: string;
  generatedLabel: string;
  agency: AgencyBrand;
  sections: PdfSection[];
  landscape?: boolean;
  fileName: string;
}

function hexToRgb(hex: string): [number, number, number] {
  const h = hex.replace('#', '');
  return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
}

/** Gera o PDF com a identidade visual da agência (cor, nome, rodapé). jsPDF é carregado sob demanda. */
export async function buildPdf(r: PdfReport): Promise<void> {
  const [{ jsPDF }, { default: autoTable }] = await Promise.all([import('jspdf'), import('jspdf-autotable')]);
  const doc = new jsPDF({ orientation: r.landscape ? 'landscape' : 'portrait', unit: 'mm', format: 'a4' });
  const W = doc.internal.pageSize.getWidth();
  const H = doc.internal.pageSize.getHeight();
  const M = 14;
  const brand = hexToRgb(r.agency.primaryColor || '#6941C6');

  // Cabeçalho.
  doc.setFillColor(16, 24, 40);
  doc.rect(0, 0, W, 30, 'F');
  doc.setFillColor(...brand);
  doc.rect(0, 30, W, 1.5, 'F');
  doc.setTextColor(255, 255, 255);
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(16);
  doc.text(r.title, M, 13);
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(9.5);
  doc.text(r.subtitle, M, 20);
  doc.setFontSize(11);
  doc.setFont('helvetica', 'bold');
  doc.text(r.agency.agencyName, W - M, 13, { align: 'right' });
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  doc.text(r.generatedLabel, W - M, 20, { align: 'right' });

  let y = 40;
  const ensure = (h: number) => { if (y + h > H - 16) { doc.addPage(); y = 18; } };

  for (const s of r.sections) {
    if (s.title) {
      ensure(12);
      doc.setTextColor(...brand);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(12);
      doc.text(s.title, M, y);
      y += 6;
    }
    if (s.text) {
      doc.setTextColor(71, 84, 103);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(9);
      const lines = doc.splitTextToSize(s.text, W - 2 * M);
      ensure(lines.length * 4.5);
      doc.text(lines, M, y);
      y += lines.length * 4.5 + 2;
    }
    if (s.kpis?.length) {
      const cols = r.landscape ? 5 : 4;
      const gap = 3;
      const w = (W - 2 * M - gap * (cols - 1)) / cols;
      s.kpis.forEach((k, i) => {
        const col = i % cols;
        if (col === 0) ensure(20);
        const x = M + col * (w + gap);
        doc.setFillColor(249, 250, 251);
        doc.setDrawColor(234, 236, 240);
        doc.roundedRect(x, y, w, 17, 2, 2, 'FD');
        doc.setTextColor(102, 112, 133);
        doc.setFontSize(7.5);
        doc.setFont('helvetica', 'normal');
        doc.text(k.label, x + 3, y + 5);
        doc.setTextColor(16, 24, 40);
        doc.setFontSize(11.5);
        doc.setFont('helvetica', 'bold');
        doc.text(k.value, x + 3, y + 11.5);
        if (k.sub) {
          doc.setFontSize(7);
          doc.setFont('helvetica', 'normal');
          doc.setTextColor(102, 112, 133);
          doc.text(k.sub, x + 3, y + 15.3);
        }
        if (col === cols - 1 || i === s.kpis!.length - 1) y += 20;
      });
      y += 2;
    }
    if (s.images?.length) {
      const perRow = Math.min(2, s.images.length);
      const w = (W - 2 * M - 6 * (perRow - 1)) / perRow;
      const h = w * 0.5;
      s.images.forEach((img, i) => {
        const col = i % perRow;
        if (col === 0) ensure(h + 9);
        const x = M + col * (w + 6);
        doc.setTextColor(52, 64, 84);
        doc.setFontSize(8.5);
        doc.setFont('helvetica', 'bold');
        doc.text(img.caption, x, y + 3);
        try { doc.addImage(img.data, 'PNG', x, y + 5, w, h); } catch { /* imagem indisponível */ }
        if (col === perRow - 1 || i === s.images!.length - 1) y += h + 10;
      });
    }
    if (s.bullets?.length) {
      doc.setFontSize(9);
      for (const b of s.bullets) {
        const lines = doc.splitTextToSize(b.text, W - 2 * M - 6);
        ensure(lines.length * 4.6 + 2);
        const c: [number, number, number] = b.tone === 'positive' ? [18, 183, 106] : b.tone === 'negative' ? [240, 68, 56] : [46, 144, 250];
        doc.setFillColor(...c);
        doc.circle(M + 1.5, y - 1.2, 1.2, 'F');
        doc.setTextColor(52, 64, 84);
        doc.setFont('helvetica', 'normal');
        doc.text(lines, M + 5, y);
        y += lines.length * 4.6 + 1.5;
      }
      y += 2;
    }
    if (s.table) {
      autoTable(doc, {
        head: [s.table.head], body: s.table.body, foot: s.table.foot ? [s.table.foot] : undefined, startY: y,
        margin: { left: M, right: M }, styles: { fontSize: 7.5, cellPadding: 1.8 },
        headStyles: { fillColor: brand, textColor: 255 }, footStyles: { fillColor: [242, 244, 247], textColor: [16, 24, 40], fontStyle: 'bold' },
        alternateRowStyles: { fillColor: [249, 250, 251] },
        columnStyles: Object.fromEntries(s.table.head.map((_, i) => [i, { halign: i === 0 ? 'left' : 'right' }]))
      });
      y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 8;
    }
  }

  // Rodapé com numeração em todas as páginas.
  const pages = doc.getNumberOfPages();
  for (let i = 1; i <= pages; i++) {
    doc.setPage(i);
    doc.setDrawColor(234, 236, 240);
    doc.line(M, H - 11, W - M, H - 11);
    doc.setFontSize(7.5);
    doc.setTextColor(152, 162, 179);
    doc.text(r.agency.reportFooter ?? r.agency.agencyName, M, H - 6);
    doc.text(`${i} / ${pages}`, W - M, H - 6, { align: 'right' });
  }
  doc.save(r.fileName);
}
