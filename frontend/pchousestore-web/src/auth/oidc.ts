import { InMemoryWebStorage, UserManager, WebStorageStateStore } from "oidc-client-ts";

const authority = import.meta.env.VITE_OIDC_AUTHORITY ?? "https://localhost:7267/";
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID ?? "pchousestore-spa";
const redirectUri = `${window.location.origin}/auth/callback`;
const silentRedirectUri = `${window.location.origin}/auth/silent-callback`;
const postLogoutRedirectUri = `${window.location.origin}/auth/logout-callback`;
export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7267";

let tokenRenewal: Promise<string | null> | undefined;

export const userManager = new UserManager({
  authority,
  client_id: clientId,
  redirect_uri: redirectUri,
  silent_redirect_uri: silentRedirectUri,
  post_logout_redirect_uri: postLogoutRedirectUri,
  response_type: "code",
  scope: "openid profile shop.api",
  userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
  stateStore: new WebStorageStateStore({ store: window.sessionStorage, prefix: "pc-house-oidc." }),
  automaticSilentRenew: true,
  monitorSession: false,
});

export async function getShopAccessToken(): Promise<string | null> {
  const user = await userManager.getUser();
  if (user && !user.expired) return user.access_token;

  tokenRenewal ??= userManager.signinSilent()
    .then(renewedUser => renewedUser && !renewedUser.expired ? renewedUser.access_token : null)
    .catch(async () => {
      await userManager.removeUser();
      return null;
    })
    .finally(() => { tokenRenewal = undefined; });

  return tokenRenewal;
}
