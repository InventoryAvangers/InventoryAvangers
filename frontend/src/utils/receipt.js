import { jsPDF } from 'jspdf';

/**
 * Generate and download a PDF receipt for a given sale.
 *
 * @param {object} sale        - Sale document (from API or post-checkout response)
 * @param {object} shopBranding - Shop branding object from auth store
 * @param {function} fmt       - Currency formatter function (e.g. (v) => formatCurrency(v, currency))
 */
export function downloadReceiptPDF(sale, shopBranding, fmt) {
  if (!sale) return;
  const doc = new jsPDF({ format: 'a6', unit: 'mm' });
  const margin = 10;
  let y = margin;
  const saleId = sale._id ? `#${String(sale._id).slice(-8).toUpperCase()}` : 'N/A';

  const shopName = shopBranding?.shopName || shopBranding?.name || 'Inventory Avengers';

  doc.setFontSize(14);
  doc.setFont('helvetica', 'bold');
  doc.text(shopName, 74, y, { align: 'center' });
  y += 6;
  doc.setFontSize(9);
  doc.setFont('helvetica', 'normal');
  doc.text(`Receipt #${sale.receiptNumber || sale._id || 'N/A'}`, 74, y, { align: 'center' });
  y += 5;
  doc.text(`Sale ID: ${saleId}`, 74, y, { align: 'center' });
  y += 5;
  doc.text(new Date(sale.createdAt || Date.now()).toLocaleString(), 74, y, { align: 'center' });
  y += 7;
  doc.text(`Customer: ${sale.customerName || 'Walk-in'}`, margin, y); y += 5;
  doc.text(`Payment: ${(sale.paymentMethod || '').toUpperCase()}`, margin, y); y += 7;

  doc.setFont('helvetica', 'bold');
  doc.text('Item', margin, y);
  doc.text('Qty', 90, y, { align: 'right' });
  doc.text('Price', 114, y, { align: 'right' });
  doc.text('Total', 138, y, { align: 'right' });
  y += 2;
  doc.line(margin, y, 148 - margin, y);
  y += 4;

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  for (const it of (sale.items || [])) {
    doc.text(String(it.name).substring(0, 30), margin, y);
    doc.text(String(it.qty), 90, y, { align: 'right' });
    doc.text(fmt(it.price), 114, y, { align: 'right' });
    doc.text(fmt(it.price * it.qty), 138, y, { align: 'right' });
    y += 5;
  }
  y += 2;
  doc.line(margin, y, 148 - margin, y);
  y += 4;

  doc.setFontSize(9);
  // subtotal is stored on the sale; fall back to totalAmount when missing (tax is 0 in this system)
  const subtotal = sale.subtotal || sale.totalAmount;
  doc.text(`Subtotal: ${fmt(subtotal)}`, 138, y, { align: 'right' }); y += 5;
  doc.text(`Tax: ${fmt(sale.tax || 0)}`, 138, y, { align: 'right' }); y += 5;
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(10);
  doc.text(`Total: ${fmt(sale.totalAmount)}`, 138, y, { align: 'right' }); y += 8;
  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  const footerMsg = shopBranding?.receiptFooter || 'Thank you for your purchase!';
  doc.text(footerMsg, 74, y, { align: 'center' }); y += 5;
  if (shopBranding?.address) { doc.text(shopBranding.address, 74, y, { align: 'center' }); y += 5; }
  if (shopBranding?.phone) { doc.text(`Tel: ${shopBranding.phone}`, 74, y, { align: 'center' }); y += 4; }
  if (shopBranding?.email) { doc.text(shopBranding.email, 74, y, { align: 'center' }); }

  doc.save(`receipt-${sale.receiptNumber || sale._id || 'sale'}.pdf`);
}
