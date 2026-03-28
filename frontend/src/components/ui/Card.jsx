import './Card.css';

/**
 * Card — KPI summary card using global .card class.
 * Renders an icon badge, a title, a bold value, and optional children.
 * Props:
 *   title      — label shown above the value
 *   value      — main metric value
 *   icon       — React icon element
 *   color      — color variant: 'indigo' | 'emerald' | 'blue' | 'amber' (default: 'indigo')
 *   children   — optional content rendered below the value
 */
export default function Card({ title, value, icon, color = 'indigo', children }) {
  return (
    <div className="card card-kpi">
      <div className="card-kpi-header">
        <span className="card-kpi-label">{title}</span>
        {icon && (
          <div className={`card-kpi-icon card-kpi-icon--${color}`}>
            {icon}
          </div>
        )}
      </div>
      {value !== undefined && (
        <div className="card-kpi-value">{value}</div>
      )}
      {children}
    </div>
  );
}
