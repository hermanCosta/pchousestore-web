import { useEffect, useState } from "react";
import { getShopAccessToken, userManager } from "../auth/oidc";

let callbackProcessing: Promise<void> | undefined;

export default function App() {
  const [status, setStatus] = useState("Verificando sessão…");
  const [error, setError] = useState<string | null>(null);

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
          if (active) setStatus("Nenhuma Shop conectada");
          return;
        }
        const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7267"}/api/v1/session`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (!response.ok) throw new Error("A sessão expirou ou foi revogada. Entre novamente.");
        if (active) setStatus("Shop autenticada");
      } catch (cause) {
        if (active) setError(cause instanceof Error ? cause.message : "Falha ao autenticar.");
      }
    }
    void initialize();
    return () => { active = false; };
  }, []);

  if (window.location.pathname === "/auth/callback") {
    return <main className="foundation-shell"><h1>Concluindo autenticação…</h1>{error && <p role="alert">{error}</p>}</main>;
  }

  return (
    <main className="foundation-shell">
      <p className="foundation-eyebrow">pcHouseStore Web</p>
      <h1>Acesso à Shop</h1>
      <p>{status}</p>
      {error && <p role="alert">{error}</p>}
      <button onClick={() => void userManager.signinRedirect()}>Entrar</button>
      <button onClick={() => void userManager.signoutRedirect()}>Sair</button>
    </main>
  );
}
