/**
 * FullPageLoader — full-screen centered spinner shown during initial auth/data loading.
 * Uses global .full-page-loader class.
 */
import LoadingSpinner from './LoadingSpinner.jsx';

export default function FullPageLoader({ text = 'Loading...' }) {
  return (
    <div className="full-page-loader">
      <LoadingSpinner size="lg" text={text} />
    </div>
  );
}
