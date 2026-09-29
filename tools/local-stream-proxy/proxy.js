// Proxy local de streaming (mesma ideia do streaming server do Stremio).
//
// Roda na máquina de quem usa o CMS (IP residencial) e repassa os vídeos http:// pro
// player. Resolve os dois bloqueios de produção:
//  - IP de datacenter do Render bloqueado (bestcine, painéis Xtream, froststream):
//    aqui as requisições saem do IP da sua casa.
//  - Mixed content: a página do CMS é https e não pode tocar http://IP/...mp4, mas
//    http://127.0.0.1 é tratado como origem segura pelo navegador.
//
// Uso: node proxy.js   (Node 18+, sem dependências)
// Variáveis opcionais: PORT (padrão 11480), ALLOWED_ORIGINS (separadas por vírgula).

const http = require('node:http')
const https = require('node:https')
const net = require('node:net')

const PORT = Number(process.env.PORT) || 11480
const ALLOWED_ORIGINS = (process.env.ALLOWED_ORIGINS || 'https://chelifetv.onrender.com,http://localhost:5003')
    .split(',')
    .map(origin => origin.trim())
    .filter(Boolean)
const USER_AGENT = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
const MAX_REDIRECTS = 10
const IDLE_TIMEOUT_MS = 30000
const FORWARDED_RESPONSE_HEADERS = ['content-type', 'content-length', 'content-range', 'accept-ranges', 'last-modified', 'etag']

// CORS só pras origens do CMS (o /health é lido via fetch). O <video> não precisa de CORS.
function getCorsHeaders(origin) {
    if (!origin || !ALLOWED_ORIGINS.includes(origin)) return {}
    return {
        'Access-Control-Allow-Origin': origin,
        'Access-Control-Allow-Headers': 'Range',
        'Access-Control-Allow-Private-Network': 'true',
        'Access-Control-Expose-Headers': 'Content-Length, Content-Range, Accept-Ranges',
        'Vary': 'Origin'
    }
}

// Não deixa o proxy virar ponte pra rede interna (roteador, outros serviços locais).
function isPrivateHost(hostname) {
    const host = hostname.replace(/^\[|\]$/g, '').toLowerCase()
    if (host === 'localhost' || host.endsWith('.localhost')) return true
    if (net.isIPv4(host)) {
        const [a, b] = host.split('.').map(Number)
        return a === 10 || a === 127 || a === 0
            || (a === 169 && b === 254)
            || (a === 172 && b >= 16 && b <= 31)
            || (a === 192 && b === 168)
    }
    if (net.isIPv6(host)) return host === '::1' || host.startsWith('fc') || host.startsWith('fd') || host.startsWith('fe80')
    return false
}

function parseTargetUrl(value) {
    try {
        const url = new URL(value)
        if (url.protocol !== 'http:' && url.protocol !== 'https:') return null
        if (isPrivateHost(url.hostname)) return null
        return url
    } catch {
        return null
    }
}

// Segue os redirects manualmente (inclusive https -> http, ex: bestcine /r/<hash>)
// e entrega a resposta final. Repassa o Range pro seek funcionar.
function openUpstream(targetUrl, method, range, redirectsLeft, onResponse, onError) {
    const client = targetUrl.protocol === 'https:' ? https : http
    const headers = { 'User-Agent': USER_AGENT, 'Accept': '*/*' }
    if (range) headers.Range = range

    const upstreamRequest = client.request(targetUrl, { method, headers }, upstreamResponse => {
        const { statusCode, headers: responseHeaders } = upstreamResponse
        const isRedirect = statusCode >= 300 && statusCode < 400 && responseHeaders.location

        if (!isRedirect) return onResponse(upstreamResponse)

        upstreamResponse.resume()
        const nextUrl = parseTargetUrl(new URL(responseHeaders.location, targetUrl).toString())
        if (!nextUrl || redirectsLeft <= 0) return onError(new Error('Redirect inválido ou em excesso'))

        openUpstream(nextUrl, method, range, redirectsLeft - 1, onResponse, onError)
    })

    upstreamRequest.setTimeout(IDLE_TIMEOUT_MS, () => upstreamRequest.destroy(new Error('timeout')))
    upstreamRequest.on('error', onError)
    upstreamRequest.end()
}

function handleStream(request, response, requestUrl, corsHeaders) {
    const targetUrl = parseTargetUrl(requestUrl.searchParams.get('url') || '')
    if (!targetUrl) {
        response.writeHead(400, corsHeaders).end('url inválida')
        return
    }

    const method = request.method === 'HEAD' ? 'HEAD' : 'GET'
    openUpstream(targetUrl, method, request.headers.range, MAX_REDIRECTS,
        upstreamResponse => {
            const headers = { ...corsHeaders }
            FORWARDED_RESPONSE_HEADERS.forEach(name => {
                if (upstreamResponse.headers[name]) headers[name] = upstreamResponse.headers[name]
            })

            response.writeHead(upstreamResponse.statusCode, headers)
            upstreamResponse.pipe(response)
            // Player fechou/pulou (seek): corta o download da origem
            response.on('close', () => upstreamResponse.destroy())
        },
        error => {
            console.warn(`[proxy] falha em ${targetUrl.host}: ${error.message}`)
            if (!response.headersSent) response.writeHead(502, corsHeaders).end(error.message)
            else response.destroy()
        })
}

const server = http.createServer((request, response) => {
    const requestUrl = new URL(request.url, `http://${request.headers.host}`)
    const corsHeaders = getCorsHeaders(request.headers.origin)

    if (request.method === 'OPTIONS') {
        response.writeHead(204, corsHeaders).end()
        return
    }

    if (requestUrl.pathname === '/health') {
        response.writeHead(200, { ...corsHeaders, 'Content-Type': 'application/json' }).end('{"ok":true}')
        return
    }

    // /stream/media.<ext>?url=... - a extensão no path é só pro video.js aceitar a fonte
    if (requestUrl.pathname.startsWith('/stream/') && (request.method === 'GET' || request.method === 'HEAD')) {
        handleStream(request, response, requestUrl, corsHeaders)
        return
    }

    response.writeHead(404, corsHeaders).end()
})

// Só loopback: ninguém da rede local acessa o proxy
server.listen(PORT, '127.0.0.1', () => {
    console.log(`Proxy local rodando em http://127.0.0.1:${PORT}`)
    console.log(`Origens liberadas: ${ALLOWED_ORIGINS.join(', ')}`)
})
