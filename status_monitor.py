#!/usr/bin/env python3
"""Optional companion process: mark the existing Discord embed offline on heartbeat loss."""
import argparse
import json
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import quote, urlsplit, urlunsplit
from urllib.request import Request, urlopen


def pending_update(path, timeout, now, last_sent):
    """Return a stale snapshot once per successful edit; ignore incomplete heartbeat writes."""
    try:
        snapshot = json.loads(Path(path).read_text(encoding='utf-8'))
        if not snapshot.get('enabled', False):
            return None
        updated = datetime.fromisoformat(snapshot['updated_at'].replace('Z', '+00:00'))
        if updated.tzinfo is None or (now - updated).total_seconds() < timeout:
            return None
        message_id = snapshot['message_id']
        payload = snapshot['offline_payload']
        if not isinstance(message_id, str) or not message_id.isdigit():
            return None
        if not isinstance(payload, dict) or not isinstance(payload.get('embeds'), list):
            return None
        key = (message_id, snapshot['updated_at'])
        if key == last_sent:
            return None
        marker = snapshot.get('offline_activity_marker', '')
        for embed in payload['embeds']:
            description = embed.get('description', '')
            suffix = ' • ' + marker
            if marker and isinstance(description, str) and description.endswith(suffix):
                detected_at = int(now.timestamp())
                embed['description'] = description[:-len(suffix)] + ' • <t:%s:R>' % detected_at
            if 'timestamp' in embed:
                embed['timestamp'] = now.astimezone(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
        return key, message_id, payload
    except (OSError, ValueError, KeyError, TypeError, AttributeError):
        return None


def send_update(webhook, message_id, payload):
    parts = urlsplit(webhook.strip())
    if parts.scheme not in ('http', 'https') or not parts.netloc or parts.username or parts.password:
        raise ValueError('Invalid webhook URL')
    # Keep optional webhook query parameters (e.g. thread_id) while selecting the message.
    endpoint = urlunsplit((parts.scheme, parts.netloc, parts.path.rstrip('/') + '/messages/' + quote(message_id, safe=''), parts.query, ''))
    request = Request(endpoint, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json', 'User-Agent': 'ATT-JoinLeave-StatusMonitor/1.0'}, method='PATCH')
    with urlopen(request, timeout=10) as response:
        response.read()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--heartbeat', required=True, type=Path, help='Same absolute path as StatusEmbedHeartbeatPath')
    parser.add_argument('--webhook-file', type=Path, help='Private file containing the DiscordWebhookUrl; otherwise use ATT_STATUS_WEBHOOK_URL')
    parser.add_argument('--timeout', type=float, default=180, help='Seconds without a heartbeat before marking offline (default: 180)')
    parser.add_argument('--poll-seconds', type=float, default=10, help='Seconds between checks (default: 10)')
    args = parser.parse_args()
    if not (args.timeout >= 60 and 1 <= args.poll_seconds <= 300):
        parser.error('Use a timeout of at least 60 seconds and a polling interval of 1-300 seconds')
    try:
        webhook = args.webhook_file.read_text(encoding='utf-8').strip() if args.webhook_file else os.environ.get('ATT_STATUS_WEBHOOK_URL', '').strip()
        parts = urlsplit(webhook)
        if parts.scheme not in ('http', 'https') or not parts.netloc or parts.username or parts.password:
            raise ValueError()
    except (OSError, ValueError):
        parser.error('Supply a readable --webhook-file or ATT_STATUS_WEBHOOK_URL containing a valid HTTP(S) webhook URL')
    last_sent = None
    print('Status monitor started; waiting for heartbeat loss.', flush=True)
    try:
        while True:
            update = pending_update(args.heartbeat, args.timeout, datetime.now(timezone.utc), last_sent)
            if update is not None:
                key, message_id, payload = update
                try:
                    send_update(webhook, message_id, payload)
                    last_sent = key
                    print('Status embed marked offline.', flush=True)
                except Exception as error:
                    # Do not print request URLs, which contain the webhook secret.
                    print('Offline edit failed (%s); will retry.' % type(error).__name__, file=sys.stderr, flush=True)
            time.sleep(args.poll_seconds)
    except KeyboardInterrupt:
        return 0


if __name__ == '__main__':
    raise SystemExit(main())
