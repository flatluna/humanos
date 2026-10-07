export interface StepActivity {
  intervalId: string;
  elapsedMilliseconds: number;
  stopped: boolean;
}

export function createVisibleStepTimer(
  emit: (activity: StepActivity) => void,
  now: () => number = () => performance.now(),
  newId: () => string = () => crypto.randomUUID(),
) {
  let interval: { id: string; started: number } | undefined;

  function checkpoint(stopped: boolean) {
    if (!interval) return;
    emit({
      intervalId: interval.id,
      elapsedMilliseconds: Math.max(0, Math.floor(now() - interval.started)),
      stopped,
    });
    if (stopped) interval = undefined;
  }

  return {
    setVisible(visible: boolean) {
      if (!visible) {
        checkpoint(true);
      } else if (!interval) {
        interval = { id: newId(), started: now() };
        checkpoint(false);
      }
    },
    checkpoint: () => checkpoint(false),
    stop: () => checkpoint(true),
  };
}
