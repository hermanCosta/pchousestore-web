import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import EmployeeAdministration from "./EmployeeAdministration";
import { apiBaseUrl, getShopAccessToken, userManager } from "../auth/oidc";

let callbackProcessing: Promise<void> | undefined;

type ShopSession = {
  companyId: string;
  shopId: string;
  shopCode: string;
  shopName: string;
  shopAddress: string | null;
  shopPhone: string | null;
};

export default function App() {
  const [status, setStatus] = useState("Checking session…");
  const [error, setError] = useState<string | null>(null);
  const [shop, setShop] = useState<ShopSession | null>(null);
  const [action, setAction] = useState<"rotate" | "status">("rotate");
  const [employeeCode, setEmployeeCode] = useState("");
  const [employeePassword, setEmployeePassword] = useState("");
  const [credentialVersion, setCredentialVersion] = useState("1");
  const [newShopPassword, setNewShopPassword] = useState("");
  const [reason, setReason] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    async function initialize() {
      try {
        if (window.location.pathname === "/auth/callback") {
          callbackProcessing ??= userManager.signinRedirectCallback().then(() => {
            window.history.replaceState({}, document.title, "/");
          });
          await callbackProcessing;
        }
        const token = await getShopAccessToken();
        if (!token) {
          if (active) {
            setShop(null);
            setStatus("No Shop is signed in");
          }
          return;
        }
        const response = await fetch(`${apiBaseUrl}/api/v1/session`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (response.status === 401) throw new Error("The API rejected the access token (HTTP 401). Please sign in again.");
        if (response.status === 403) throw new Error("The access token is missing the required Shop API scope (HTTP 403).");
        if (!response.ok) throw new Error(`Session verification failed (HTTP ${response.status}).`);
        const session = await response.json() as ShopSession;
        if (active) {
          setShop(session);
          setCredentialVersion(window.sessionStorage.getItem(`pc-house-credential-version.${session.shopId}`) ?? "1");
          setStatus("Shop signed in");
        }
      } catch (cause) {
        if (active) setError(cause instanceof Error ? cause.message : "Sign-in failed.");
      }
    }
    void initialize();
    return () => { active = false; };
  }, []);

  async function handleCredentialSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!shop || saving) return;
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      const token = await getShopAccessToken();
      if (!token) throw new Error("Your Shop session has expired. Sign in again.");
      const version = Number(credentialVersion);
      if (!Number.isSafeInteger(version) || version < 1) throw new Error("Enter a valid credential version.");

      const actionCode = action === "rotate" ? "SHOP_ACCESS_CREDENTIAL_ROTATE" : "SHOP_ACCESS_CREDENTIAL_CHANGE_STATUS";
      const challenge = await fetch(`${apiBaseUrl}/api/v1/employee-action-authorizations`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({
          employeeCode,
          password: employeePassword,
          actionCode,
          resourceType: "Shop",
          resourceId: shop.shopId,
          targetShopId: Number(shop.shopId),
          operationContext: { credentialVersion: version },
        }),
      });
      if (!challenge.ok) throw await apiError(challenge);
      const authorization = await challenge.json() as { authorizationToken: string };
      const response = await fetch(`${apiBaseUrl}/api/v1/shops/${encodeURIComponent(shop.shopId)}/access-credential`, {
        method: action === "rotate" ? "PUT" : "PATCH",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
          "X-Employee-Action-Authorization": authorization.authorizationToken,
        },
        body: JSON.stringify(action === "rotate"
          ? { newPassword: newShopPassword, expectedCredentialVersion: version }
          : { isActive, expectedCredentialVersion: version, reason }),
      });
      if (!response.ok) throw await apiError(response);

      window.sessionStorage.setItem(`pc-house-credential-version.${shop.shopId}`, String(version + 1));
      setCredentialVersion(String(version + 1));
      setEmployeePassword("");
      setNewShopPassword("");
      setReason("");
      setNotice("Credential updated. All sessions for this Shop were signed out; sign in again to continue.");
      await userManager.removeUser();
      setShop(null);
      setStatus("Shop credential updated");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The credential could not be updated.");
    } finally {
      setEmployeePassword("");
      setNewShopPassword("");
      setSaving(false);
    }
  }

  if (window.location.pathname === "/auth/callback") {
    return (
      <main className="auth-shell">
        <section className="auth-card" aria-live="polite">
          <Brand />
          <div className="auth-content">
            <h1>Completing sign-in</h1>
            <p className="auth-description">Please wait while your Shop session is verified.</p>
            {error && (
              <div className="auth-error" role="alert">
                <p>{error}</p>
                <button onClick={() => void userManager.signinRedirect()}>Restart sign-in</button>
              </div>
            )}
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="auth-shell">
      <section className={shop ? "auth-card auth-card-wide" : "auth-card"}>
        <Brand />
        <div className="auth-content">
          <h1>Shop access</h1>
          <p className="auth-description">
            {shop
              ? "Your Shop session is active. Manage this Shop's access credential below."
              : "Sign in to access your Shop workspace."}
          </p>
          <p className="auth-status" aria-live="polite">
            {shop ? `Signed in to ${shop.shopName} (${shop.shopCode})` : status}
          </p>
          {shop && (
            <dl className="shop-profile" aria-label="Active Shop profile">
              <div><dt>Shop</dt><dd>{shop.shopName}</dd></div>
              <div><dt>Address</dt><dd>{shop.shopAddress || "Not set"}</dd></div>
              <div><dt>Phone</dt><dd>{shop.shopPhone || "Not set"}</dd></div>
            </dl>
          )}
          {error && <p className="auth-error" role="alert">{error}</p>}
          {notice && <p className="auth-notice" role="status">{notice}</p>}
          {shop && (
            <form className="auth-form credential-form" onSubmit={handleCredentialSubmit}>
              <h2>Shop access credential</h2>
              <p className="form-help">Changes apply to {shop.shopName}. You will need to sign in again after saving.</p>
              <label className="auth-field">
                Credential version
                <input type="number" min="1" step="1" required value={credentialVersion} onChange={event => setCredentialVersion(event.target.value)} />
              </label>
              <p className="form-help">This version is remembered in this browser after a successful update. If another Manager changed the credential, enter its current version and request fresh verification.</p>
              <label className="auth-field">
                Operation
                <select value={action} onChange={event => setAction(event.target.value as "rotate" | "status")}>
                  <option value="rotate">Change Shop password</option>
                  <option value="status">Change credential status</option>
                </select>
              </label>
              {action === "rotate" ? (
                <label className="auth-field">
                  New Shop password
                  <input type="password" minLength={12} maxLength={128} autoComplete="new-password" required value={newShopPassword} onChange={event => setNewShopPassword(event.target.value)} />
                </label>
              ) : (
                <>
                  <label className="auth-field">
                    Status
                    <select value={isActive ? "active" : "disabled"} onChange={event => setIsActive(event.target.value === "active")}>
                      <option value="active">Enabled</option>
                      <option value="disabled">Disabled</option>
                    </select>
                  </label>
                  <label className="auth-field">
                    Reason
                    <input required maxLength={500} value={reason} onChange={event => setReason(event.target.value)} />
                  </label>
                </>
              )}
              <hr />
              <h2>Manager verification</h2>
              <p className="form-help">Enter your employee credentials for this operation. The password is used once and cleared after submission.</p>
              <label className="auth-field">
                Employee code
                <input autoComplete="username" required maxLength={40} value={employeeCode} onChange={event => setEmployeeCode(event.target.value)} />
              </label>
              <label className="auth-field">
                Employee password
                <input type="password" autoComplete="current-password" required maxLength={128} value={employeePassword} onChange={event => setEmployeePassword(event.target.value)} />
              </label>
              <button className="auth-submit" disabled={saving}>{saving ? "Saving…" : "Save credential"}</button>
            </form>
          )}
          {shop && <EmployeeAdministration companyId={shop.companyId} getShopAccessToken={getShopAccessToken} />}
          <div className="auth-actions">
            {!shop && <button onClick={() => void userManager.signinRedirect()}>Sign in</button>}
            {shop && <button className="secondary" onClick={() => void userManager.signoutRedirect()}>Sign out</button>}
          </div>
        </div>
      </section>
    </main>
  );
}

async function apiError(response: Response): Promise<Error> {
  try {
    const problem = await response.json() as { detail?: string; code?: string };
    return new Error(problem.detail ?? problem.code ?? `Request failed (HTTP ${response.status}).`);
  } catch {
    return new Error(`Request failed (HTTP ${response.status}).`);
  }
}

function Brand() {
  return (
    <div className="auth-brand">
      <img src={`${apiBaseUrl}/assets/pc-house-logo.png`} alt="PC House logo" />
      <span>PC HOUSE STORE</span>
    </div>
  );
}
