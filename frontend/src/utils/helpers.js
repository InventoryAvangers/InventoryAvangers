export function fmt(n) {
  return Number(n || 0).toLocaleString('en-CA', {
    style: 'currency',
    currency: 'CAD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

export function formatCurrency(amount, currencyCode = 'INR') {
  const CURRENCY_LOCALES = { INR: 'en-IN', USD: 'en-US', EUR: 'de-DE', GBP: 'en-GB', CAD: 'en-CA' };
  const code = currencyCode || 'INR';
  const locale = CURRENCY_LOCALES[code] || 'en-US';
  return Number(amount || 0).toLocaleString(locale, {
    style: 'currency',
    currency: code,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

export function fmtDate(d) {
  return new Date(d).toLocaleString('en-CA', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function fmtShortDate(d) {
  return new Date(d).toLocaleString('en-CA', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function getDayKey(date) {
  return new Date(date).toLocaleDateString('en-CA', { month: 'short', day: 'numeric' });
}

export function getLast7Days() {
  const days = [];
  for (let i = 6; i >= 0; i--) {
    const d = new Date();
    d.setDate(d.getDate() - i);
    days.push(d);
  }
  return days;
}
