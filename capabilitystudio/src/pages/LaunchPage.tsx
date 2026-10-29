import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { saveLaunchSession } from '../auth/launchSession';

interface ExchangeResponse {
  token: string;
}

const DEFAULT_EXCHANGE_URL = 'http://localhost:7072/api/studio-launch/exchange';

/** Entry point for Studio when opened from a parent app (2026-08-19 —
 *  Studio no longer supports a standalone MSAL login; this is the only
 *  way in). The parent app's backend issues a one-time `code` (60s TTL)
 *  and redirects the admin's browser here; we immediately exchange it
 *  for a signed JWT ("launch session") and drop the code from history so
 *  it can't be reused or bookmarked. Any parent app can reuse this by
 *  implementing the same create-launch/exchange endpoint pair — see
 *  docs/STUDIO_LAUNCH_INTEGRATION.md — and optionally passing its own
 *  `exchangeUrl` query param if it isn't the NGO backend. */
export default function LaunchPage() {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const exchanged = useRef(false);

  useEffect(() => {
    if (exchanged.current) return;
    exchanged.current = true;

    const code = searchParams.get('code');
    const exchangeUrl = searchParams.get('exchangeUrl') ?? DEFAULT_EXCHANGE_URL;
    const returnTo = searchParams.get('returnTo') || '/';

    if (!code) {
      setError(t('launchPage.missingCode'));
      return;
    }

    (async () => {
      try {
        const response = await fetch(exchangeUrl, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code }),
        });

        if (!response.ok) {
          setError(t('launchPage.invalidOrExpired'));
          return;
        }

        const data = (await response.json()) as ExchangeResponse;
        const session = saveLaunchSession(data.token);
        if (!session) {
          setError(t('launchPage.invalidToken'));
          return;
        }
        navigate(returnTo, { replace: true });
      } catch {
        setError(t('launchPage.validationFailed'));
      }
    })();
  }, [navigate, searchParams]);


  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-950 px-4 text-center">
      {error ? (
        <p className="max-w-md text-sm text-slate-400">{error}</p>
      ) : (
        <>
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-700 border-t-brand-400" />
          <p className="text-sm text-slate-400">{t('launchPage.validating')}</p>
        </>
      )}
    </div>
  );
}
