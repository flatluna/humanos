import { Navigate } from 'react-router';
import { useAuth } from '@/auth/AuthContext';

export function ProfileRedirectPage() {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-300 border-t-brand-600 dark:border-white/20 dark:border-t-brand-400" />
      </div>
    );
  }

  if (user?.personId) {
    return <Navigate to={`/personal/${user.personId}`} replace />;
  }

  return <Navigate to="/personal" replace />;
}
