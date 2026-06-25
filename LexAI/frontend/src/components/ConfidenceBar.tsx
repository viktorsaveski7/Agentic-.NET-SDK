import type { FC } from "react";

interface Props {
  confidence: number;
}

const ConfidenceBar: FC<Props> = ({ confidence }) => {
  const clamped = Math.max(0, Math.min(1, confidence));
  const pct = Math.round(clamped * 100);

  const level = clamped >= 0.8 ? "high" : clamped >= 0.6 ? "medium" : "low";
  const color =
    level === "high" ? "bg-high" : level === "medium" ? "bg-medium" : "bg-low";
  const label =
    level === "high"
      ? "Висока доверба"
      : level === "medium"
      ? "Средна доверба"
      : "Ниска доверба";

  return (
    <div className="mt-3">
      <div className="mb-1 flex justify-between text-xs text-text-secondary">
        <span>{label}</span>
        <span>{pct}%</span>
      </div>
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-border">
        <div
          className={`h-full ${color} transition-all duration-500`}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
};

export default ConfidenceBar;
