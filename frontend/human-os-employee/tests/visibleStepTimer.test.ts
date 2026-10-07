import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { createVisibleStepTimer, type StepActivity } from '../src/features/capabilities/hooks/visibleStepTimer.ts';

test('counts five visible seconds, excludes a hidden minute, and sums the return', () => {
  let clock = 0;
  let id = 0;
  const events: StepActivity[] = [];
  const timer = createVisibleStepTimer((event) => events.push(event), () => clock, () => String(++id));
  timer.setVisible(true);
  clock = 5_000;
  timer.setVisible(false);
  clock = 65_000;
  timer.checkpoint();
  timer.setVisible(true);
  clock = 68_000;
  timer.stop();
  assert.deepEqual(events.filter((event) => event.stopped).map((event) => event.elapsedMilliseconds), [5_000, 3_000]);
  assert.equal(new Set(events.map((event) => event.intervalId)).size, 2);
});

test('heartbeats are cumulative and repeated visibility events do not restart the clock', () => {
  let clock = 0;
  const events: StepActivity[] = [];
  const timer = createVisibleStepTimer((event) => events.push(event), () => clock, () => 'interval');
  timer.setVisible(false);
  timer.setVisible(true);
  clock = 10_000;
  timer.checkpoint();
  timer.setVisible(true);
  clock = 20_000;
  timer.checkpoint();
  clock = 21_500;
  timer.stop();
  timer.stop();
  assert.deepEqual(events.map((event) => event.elapsedMilliseconds), [0, 10_000, 20_000, 21_500]);
});

test('StrictMode setup and cleanup never count the gap between intervals', () => {
  let clock = 0;
  let id = 0;
  const events: StepActivity[] = [];
  const timer = createVisibleStepTimer((event) => events.push(event), () => clock, () => String(++id));
  timer.setVisible(true);
  timer.stop();
  clock = 500;
  timer.setVisible(true);
  clock = 5_500;
  timer.stop();
  assert.deepEqual(events.filter((event) => event.stopped).map((event) => event.elapsedMilliseconds), [0, 5_000]);
});
