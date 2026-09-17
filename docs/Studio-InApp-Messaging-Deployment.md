# Aegis.Studio In-App Messaging Deployment and Verification

This runbook covers the Studio staff messaging feature: direct, workspace and job threads; unread/read state; mentions; live toasts; SignalR realtime delivery; and the polling fallback.

## Architecture

Studio owns conversation context, authorization, durable chat history, thread membership and read state. Common.Messaging owns notification delivery channels. System notifications and mentions remain distinct from chat history.

Supported Studio chat scopes:

- Direct staff-to-staff
- Workspace team
- Job team

User experience:

- unread counts
- durable history
- `@user` autocomplete and highlighting
- distinct "You were mentioned" notifications
- live message/thread change events through SignalR
- "While You Were Away" unread digest
- 5-second polling fallback when realtime transport is unavailable

## Server endpoint

Studio maps the authenticated SignalR hub at:

```text
/hubs/messages
```

The hub joins an authenticated connection to its user group and permits joining only threads the current user is authorized to view.

## Nginx requirement

The Studio reverse proxy must support WebSocket upgrade. The main proxy location should include:

```nginx
location / {
    proxy_pass http://127.0.0.1:<studio-port>;
    proxy_http_version 1.1;

    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;

    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
}
```

Validate before reload:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

If WebSocket negotiation fails, Studio deliberately retains the 5-second polling path. Realtime must never be reported as operational merely because polling still works.

## Browser realtime behavior

The browser negotiates with:

```text
POST /hubs/messages/negotiate?negotiateVersion=1
```

and opens `ws://` or `wss://` to `/hubs/messages` using the returned connection token.

Handled server events:

- `messageReceived`
- `threadChanged`
- `mentionReceived`

When a thread is open, the client joins that thread group. When switching threads it leaves the previous group. If the socket closes, the browser retries and polling continues.

## Mention behavior

A mention is valid only when the token resolves to another actual participant of the current thread. Mention matching is performed server-side, not trusted from the browser.

For each valid mention Studio:

1. stores the chat message in the Studio message history;
2. creates a durable `Mention` notification through Studio's notification service;
3. respects the recipient's `MentionsAndReplies` preference;
4. signals the user's SignalR group with `mentionReceived`;
5. shows a distinct "You were mentioned" toast when the user is online;
6. leaves the ordinary unread/thread state intact.

A mention is not a replacement for the original message and must not create a second chat message.

## While You Were Away

Once per browser session Studio checks unread thread state. If unread conversations exist, it shows a digest with total unread messages and the affected Direct/Workspace/Job threads. Selecting a digest item opens that thread.

## Verification with two users

Use two authenticated Studio accounts with access to the same workspace/job or a direct thread.

### Direct message

1. User A opens Messages → New.
2. Select User B.
3. Send a message.
4. Confirm User B sees the unread badge and a live toast without manually refreshing.
5. Open the thread and confirm unread count clears.

### Mention

1. User A sends `@<UserB> please review this` in a shared thread.
2. Confirm User B receives a distinct `You were mentioned` toast.
3. Confirm Notifications contains a durable `Mention` event.
4. Confirm the original message remains once in the chat history.
5. Confirm a non-participant typed after `@` does not generate a mention notification.

### Workspace and job threads

1. Send a workspace-team message.
2. Confirm all active members receive `threadChanged` and appropriate unread state.
3. Repeat for a job-team thread.
4. Confirm a user without View/ViewJob permission cannot join or read the thread.

### Realtime fallback

1. Temporarily block WebSocket upgrade or use browser developer tools to prevent the socket connection.
2. Confirm messages still appear through the 5-second polling fallback.
3. Restore WebSocket support.
4. Confirm the browser reconnects and realtime delivery resumes.

## Troubleshooting

### Messages work but are delayed by up to five seconds

The polling fallback is working but SignalR/WebSocket is not. Check nginx upgrade headers, browser WebSocket connection, authentication cookie, and `/hubs/messages/negotiate`.

### SignalR returns 401/403

Confirm the Studio authentication cookie is present and the user has access to the requested thread. The hub is intentionally authorization-protected.

### Mention text is highlighted but no mention notification arrives

Confirm the mentioned username matches a real Studio user and that the user is a participant of that thread. Check the recipient's `MentionsAndReplies` preference and the durable Studio notifications table/API.

### Duplicate ordinary toast after mention toast

The browser suppresses an ordinary unread toast for a recently received mention on the same thread. If duplicates persist, inspect ordering of `mentionReceived` and `threadChanged` events.

## Acceptance checklist

- Direct, Workspace and Job threads persist after restart.
- Thread authorization is enforced server-side.
- Read/unread counts are correct.
- Direct thread title shows the other participant.
- New Message uses the Studio user picker.
- `@` autocomplete lists Studio users.
- Only valid thread participants generate mention notifications.
- Mention notifications are durable.
- SignalR delivers live events when WebSockets are available.
- Polling fallback works when SignalR is unavailable.
- While You Were Away appears once per session when appropriate.
- No chat history is stored in Common.Messaging's notification store.
- Common.Messaging remains responsible for external/in-app notification delivery, not Studio thread ownership.
