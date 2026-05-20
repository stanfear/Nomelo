import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";

export interface SelectOption {
  value: string;
  label: string;
}

interface Props {
  label: string;
  value: string;
  options: SelectOption[];
  placeholder?: string;
  required?: boolean;
  onChange: (value: string) => void;
}

export function SelectField({
  label,
  value,
  options,
  placeholder = "— choisir —",
  required = false,
  onChange,
}: Props) {
  const id = useId();
  const labelId = `${id}-label`;
  const listId = `${id}-list`;
  const [open, setOpen] = useState(false);
  const [highlight, setHighlight] = useState<number>(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  const selected = options.find((o) => o.value === value);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onPointerDown);
    return () => document.removeEventListener("mousedown", onPointerDown);
  }, [open]);

  const openWith = (initialHighlight: number) => {
    setOpen(true);
    setHighlight(initialHighlight);
  };

  const handleKey = (e: KeyboardEvent<HTMLButtonElement>) => {
    if (!open) {
      if (e.key === "Enter" || e.key === " " || e.key === "ArrowDown" || e.key === "ArrowUp") {
        e.preventDefault();
        const currentIndex = options.findIndex((o) => o.value === value);
        openWith(currentIndex >= 0 ? currentIndex : 0);
      }
      return;
    }

    if (e.key === "Escape") {
      e.preventDefault();
      setOpen(false);
      return;
    }
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setHighlight((h) => (h + 1) % options.length);
      return;
    }
    if (e.key === "ArrowUp") {
      e.preventDefault();
      setHighlight((h) => (h - 1 + options.length) % options.length);
      return;
    }
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      if (highlight >= 0 && highlight < options.length) {
        onChange(options[highlight].value);
        setOpen(false);
      }
      return;
    }
    if (e.key === "Home") {
      e.preventDefault();
      setHighlight(0);
      return;
    }
    if (e.key === "End") {
      e.preventDefault();
      setHighlight(options.length - 1);
    }
  };

  return (
    <div className="select-field" ref={containerRef}>
      <span id={labelId} className="select-field__label">
        {label}
      </span>
      <button
        ref={buttonRef}
        type="button"
        className="select-field__control"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-labelledby={labelId}
        aria-controls={listId}
        aria-required={required || undefined}
        onClick={() => {
          if (open) setOpen(false);
          else openWith(options.findIndex((o) => o.value === value));
        }}
        onKeyDown={handleKey}
      >
        <span className={selected ? "" : "select-field__placeholder"}>
          {selected ? selected.label : placeholder}
        </span>
        <svg
          className="select-field__chevron"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </button>
      {open && (
        <ul
          id={listId}
          role="listbox"
          aria-labelledby={labelId}
          className="select-field__list"
        >
          {options.length === 0 && (
            <li className="select-field__empty">Aucune option disponible</li>
          )}
          {options.map((o, i) => (
            <li
              key={o.value}
              role="option"
              aria-selected={value === o.value}
              className={[
                "select-field__option",
                highlight === i ? "is-highlighted" : "",
                value === o.value ? "is-selected" : "",
              ]
                .filter(Boolean)
                .join(" ")}
              onMouseEnter={() => setHighlight(i)}
              onMouseDown={(e) => {
                e.preventDefault();
                onChange(o.value);
                setOpen(false);
                buttonRef.current?.focus();
              }}
            >
              {o.label}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
