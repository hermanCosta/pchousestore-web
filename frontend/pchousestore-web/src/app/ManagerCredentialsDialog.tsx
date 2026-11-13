import { useEffect, useState, type FormEvent } from "react";

export type ManagerCredentials = { employeeCode: string; password: string };

type Props = {
  open: boolean;
  busy: boolean;
  error: string | null;
  onCancel: () => void;
  onSubmit: (credentials: ManagerCredentials) => Promise<boolean>;
};

export default function ManagerCredentialsDialog({ open, busy, error, onCancel, onSubmit }: Props) {
  const [employeeCode, setEmployeeCode] = useState("");
  const [password, setPassword] = useState("");

  useEffect(() => {
    if (open) {
      setEmployeeCode("");
      setPassword("");
    }
  }, [open]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await onSubmit({ employeeCode, password });
    } finally {
      setEmployeeCode("");
      setPassword("");
    }
  }

  if (!open) return null;

  return (
    <div className="dialog-backdrop">
      <section className="credential-dialog" role="dialog" aria-modal="true" aria-labelledby="manager-dialog-title">
        <div className="credential-dialog-heading">
          <span className="dialog-lock-icon" aria-hidden="true">✓</span>
          <div>
            <h2 id="manager-dialog-title">Manager verification</h2>
            <p>Confirm this protected change with your employee credentials.</p>
          </div>
        </div>
        {error && <p className="auth-error dialog-error" role="alert">{error}</p>}
        <form className="auth-form" onSubmit={event => void submit(event)}>
          <label className="auth-field">Manager employee code
            <input required maxLength={40} autoComplete="username" autoFocus value={employeeCode} onChange={event => setEmployeeCode(event.target.value)} disabled={busy} />
          </label>
          <label className="auth-field">Manager password
            <input required type="password" maxLength={128} autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} disabled={busy} />
          </label>
          <p className="form-help">These credentials are used once and cleared after submission.</p>
          <div className="dialog-actions">
            <button type="button" className="secondary" onClick={onCancel} disabled={busy}>Cancel</button>
            <button type="submit" disabled={busy}>{busy ? "Verifying…" : "Confirm change"}</button>
          </div>
        </form>
      </section>
    </div>
  );
}
