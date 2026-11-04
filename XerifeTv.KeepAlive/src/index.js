const REQUEST_TIMEOUT_MS = 90_000;

async function pingHealth(healthUrl) {
  const startedAt = Date.now();
  try {
    const response = await fetch(healthUrl, {
      headers: { "User-Agent": "xerifetv-keep-alive" },
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
    console.log(`keep-alive ${response.status} em ${Date.now() - startedAt}ms`);
  } catch (error) {
    console.error(`keep-alive falhou em ${Date.now() - startedAt}ms: ${error}`);
  }
}

export default {
  async scheduled(_controller, env, ctx) {
    ctx.waitUntil(pingHealth(env.HEALTH_URL));
  },
};
