"""Checks the addresses in wallets.json the way a wallet would, so a typo is caught before it ships.
  Bitcoin   legacy Base58Check or bech32/bech32m (checksum)
  Tron      Base58Check with the 0x41 prefix (checksum)
  Solana    Base58, must decode to exactly 32 bytes
  Ethereum  0x + 40 hex; a mixed-case address must also match its EIP-55 checksum (needs `pip install pycryptodome`)
Run:  python donate/check.py            (exit code 1 if anything is wrong)
"""
import hashlib, json, os, re, sys

B58 = '123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz'


def b58decode(s):
    n = 0
    for c in s:
        if c not in B58:
            raise ValueError(f'character {c!r} is not Base58')
        n = n * 58 + B58.index(c)
    raw = n.to_bytes((n.bit_length() + 7) // 8, 'big')
    return b'\x00' * (len(s) - len(s.lstrip('1'))) + raw


def b58check(s):
    raw = b58decode(s)
    body, check = raw[:-4], raw[-4:]
    if hashlib.sha256(hashlib.sha256(body).digest()).digest()[:4] != check:
        raise ValueError('Base58Check checksum does not match')
    return body


CHARSET = 'qpzry9x8gf2tvdw0s3jn54khce6mua7l'


def bech32_ok(s):
    s = s.lower()
    hrp, data = s.rsplit('1', 1)
    vals = [CHARSET.index(c) for c in data]
    gen = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3]
    chk = 1
    for v in [ord(x) >> 5 for x in hrp] + [0] + [ord(x) & 31 for x in hrp] + vals:
        top = chk >> 25
        chk = (chk & 0x1ffffff) << 5 ^ v
        for i in range(5):
            chk ^= gen[i] if (top >> i) & 1 else 0
    if chk not in (1, 0x2bc830a3):   # bech32, bech32m
        raise ValueError('bech32 checksum does not match')


def check(w):
    a, t, net = w['address'], w['ticker'], w['network']
    if t == 'BTC':
        if a.lower().startswith('bc1'):
            bech32_ok(a)
        else:
            body = b58check(a)
            if body[0] not in (0x00, 0x05) or len(body) != 21:
                raise ValueError('not a Bitcoin mainnet address')
    elif 'TRON' in net.upper():
        body = b58check(a)
        if len(body) != 21 or body[0] != 0x41 or not a.startswith('T') or len(a) != 34:
            raise ValueError('not a Tron address')
    elif 'SOLANA' in net.upper():
        if len(b58decode(a)) != 32:
            raise ValueError('a Solana address must decode to 32 bytes')
    elif 'ETHEREUM' in net.upper():
        if not re.fullmatch(r'0x[0-9a-fA-F]{40}', a):
            raise ValueError('Ethereum address must be 0x + 40 hex characters')
        body = a[2:]
        if body != body.lower() and body != body.upper():
            try:
                from Crypto.Hash import keccak
            except ImportError:
                raise ValueError('mixed-case address: install pycryptodome to verify its EIP-55 checksum')
            h = keccak.new(digest_bits=256, data=body.lower().encode()).hexdigest()
            want = ''.join(c.upper() if int(h[i], 16) >= 8 else c for i, c in enumerate(body.lower()))
            if want != body:
                raise ValueError('EIP-55 checksum does not match')
    else:
        raise ValueError('unknown coin/network: add a check for it')


def main():
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'wallets.json')
    wallets = json.load(open(path, encoding='utf-8'))['wallets']
    bad = 0
    seen = set()
    for w in wallets:
        label = f"{w['coin']} / {w['network']}"
        try:
            if w['address'] in seen:
                raise ValueError('the same address is listed twice')
            seen.add(w['address'])
            check(w)
            print(f'OK    {label}: {w["address"]}')
        except ValueError as e:
            bad += 1
            print(f'FAIL  {label}: {e}')
    sys.exit(1 if bad else 0)


main()
