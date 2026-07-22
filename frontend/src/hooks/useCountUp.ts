import { useEffect, useRef, useState } from 'react';

/**
 * Animates a numeric value from its previous to its current value.
 * Respects prefers-reduced-motion (jumps directly to the target).
 */
export function useCountUp(target: number, duration = 600): number {
  const [display, setDisplay] = useState(target);
  const fromRef = useRef(target);
  const frameRef = useRef<number>(0);

  useEffect(() => {
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const from = fromRef.current;
    if (reduced || from === target) {
      setDisplay(target);
      fromRef.current = target;
      return;
    }

    const start = performance.now();
    const tick = (now: number) => {
      const progress = Math.min(1, (now - start) / duration);
      // iOS-style ease-out
      const eased = 1 - Math.pow(1 - progress, 3);
      const value = from + (target - from) * eased;
      setDisplay(value);
      if (progress < 1) {
        frameRef.current = requestAnimationFrame(tick);
      } else {
        fromRef.current = target;
      }
    };

    frameRef.current = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frameRef.current);
  }, [target, duration]);

  return display;
}
