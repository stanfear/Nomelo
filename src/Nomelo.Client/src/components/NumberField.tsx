import { useId } from "react";

interface Props {
  label: string;
  value: number;
  min?: number;
  max?: number;
  step?: number;
  onChange: (value: number) => void;
}

export function NumberField({
  label,
  value,
  min,
  max,
  step = 1,
  onChange,
}: Props) {
  const id = useId();
  const inputId = `${id}-input`;

  const clamp = (next: number) => {
    if (min !== undefined && next < min) return min;
    if (max !== undefined && next > max) return max;
    return next;
  };

  const canIncrement = max === undefined || value < max;
  const canDecrement = min === undefined || value > min;

  return (
    <div className="number-field">
      <label htmlFor={inputId} className="number-field__label">
        {label}
      </label>
      <div className="number-field__control">
        <input
          id={inputId}
          type="number"
          inputMode="numeric"
          value={value}
          min={min}
          max={max}
          step={step}
          onChange={(e) => {
            const parsed = Number(e.target.value);
            if (!Number.isNaN(parsed)) onChange(clamp(parsed));
          }}
          className="number-field__input"
        />
        <div className="number-field__steppers">
          <button
            type="button"
            className="number-field__stepper"
            aria-label="Augmenter"
            disabled={!canIncrement}
            onClick={() => onChange(clamp(value + step))}
            tabIndex={-1}
          >
            <svg
              width="12"
              height="12"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="3"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <polyline points="6 15 12 9 18 15" />
            </svg>
          </button>
          <button
            type="button"
            className="number-field__stepper"
            aria-label="Diminuer"
            disabled={!canDecrement}
            onClick={() => onChange(clamp(value - step))}
            tabIndex={-1}
          >
            <svg
              width="12"
              height="12"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="3"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );
}
