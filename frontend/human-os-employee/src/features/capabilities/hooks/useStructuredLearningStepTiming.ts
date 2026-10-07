import { useEffect } from 'react';
import { reportStructuredLearningEngramStepActivity } from '../api/runtimeSessionApi';
import { createVisibleStepTimer } from './visibleStepTimer';

export function useStructuredLearningStepTiming(
  sessionId: string | undefined,
  sectionId: string | undefined,
  stepType: string | undefined,
) {
  useEffect(() => {
    if (!sessionId || !sectionId || !stepType) return;
    const timer = createVisibleStepTimer((activity) => {
      reportStructuredLearningEngramStepActivity(sessionId, sectionId, stepType, activity)
        .catch((err) => console.error('Could not persist visible engram step time:', err));
    });
    const updateVisibility = () => timer.setVisible(document.visibilityState === 'visible' && document.hasFocus());
    const stop = () => timer.stop();
    updateVisibility();
    const heartbeat = setInterval(timer.checkpoint, 10_000);
    document.addEventListener('visibilitychange', updateVisibility);
    window.addEventListener('focus', updateVisibility);
    window.addEventListener('blur', updateVisibility);
    window.addEventListener('pagehide', stop);
    window.addEventListener('pageshow', updateVisibility);
    return () => {
      clearInterval(heartbeat);
      document.removeEventListener('visibilitychange', updateVisibility);
      window.removeEventListener('focus', updateVisibility);
      window.removeEventListener('blur', updateVisibility);
      window.removeEventListener('pagehide', stop);
      window.removeEventListener('pageshow', updateVisibility);
      timer.stop();
    };
  }, [sessionId, sectionId, stepType]);
}
