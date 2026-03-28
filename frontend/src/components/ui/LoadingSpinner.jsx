/**
 * LoadingSpinner — animated spinner using global .spinner classes.
 */
export default function LoadingSpinner({ size = 'md', text }) {
  const sizeClass = size === 'sm' ? 'spinner-sm' : size === 'lg' ? 'spinner-lg' : 'spinner-md';
  return (
    <div className="loading-container">
      <div className={`spinner spinner-indigo ${sizeClass}`} />
      {text && <span className="loading-text">{text}</span>}
    </div>
  );
}
