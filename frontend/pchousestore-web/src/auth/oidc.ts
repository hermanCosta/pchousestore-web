import { InMemoryWebStorage, UserManager, WebStorageStateStore } from "oidc-client-ts";

const authority = import.meta.env.VITE_OIDC_AUTHORITY ?? "https://localhost:7267/";
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID ?? "pchousestore-spa";
const redirectUri = `${window.location.origin}/auth/callback`;
export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7267";

export const userManager = new UserManager({
  authority,
  client_id: clientId,
  redirect_uri: redirectUri,
  post_logout_redirect_uri: window.location.origin,
  response_type: "code",
  scope: "openid profile shop.api",
  userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
  stateStore: new WebStorageStateStore({ store: window.sessionStorage, prefix: "pc-house-oidc." }),
  automaticSilentRenew: false,
  monitorSession: false,
});

export async function getShopAccessToken(): Promise<string | null> {
  const user = await userManager.getUser();
  return user?.expired ? null : user?.access_token ?? null;
}
