# Known Valid Workflow Rule Filter Keys Lookup Table

This document serves as a manually maintained lookup table during Phase 8 implementation (prior to the Phase B Metadata Service layer going live), detailing all valid `WorkflowTrigger.Filter` keys, their data types, and designed purposes within the system.

## 1. Introduction

In Vulperonex, each stream event type maps to specific filter slots. If a filter key not listed in this document is used in a rule, the engine will raise a `Warning` log and the rule will fail to match at runtime.

---

## 2. Valid Filter Keys Registry

All valid filter keys currently supported by the engine are compiled below:

| Event Type Key (`EventTypeKey`) | Valid Filter Key | Data Type | Example & Semantics |
|---|---|---|---|
| **`user.message`** (Chat Message) | `CommandName` | String | `"!checkin"` (restricts to a specific command, supporting auto-boundary checks to prevent `!so` from matching `!sorry`) |
| | `Prefix` | String | `"!help"` (messages starting with a specific prefix) |
| **`user.donated`** (Donations/Bits) | `MinAmount` | Number | `100` (minimum Bits/amount threshold required) |
| **`user.subscribed`** (Subscriptions) | `Tier` | String | `"1000"`, `"2000"`, `"3000"` (restricts to a specific subscription tier) |
| | `IsGift` | Boolean | `true` or `false` (whether the subscription is gifted) |
| **`user.gifted_sub`** (Gifted Subs) | `Tier` | String | `"1000"` (gifted subscription tier) |
| | `MinGiftCount` | Number | `50` (minimum count of subs gifted in a single batch) |
| **`channel.raided`** (Raids) | `MinViewers` | Number | `5` (minimum viewers brought in by the raid) |
| **`reward.redeemed`** (Channel Points) | `RewardName` | String | `"Lottery Ticket"` (restricts to a specific channel point reward name) |
| **`workflow.timer`** (Timer Fired) | `TimerName` | String | `"HourlyAlert"` (restricts to a specific timer identifier) |

---

## 3. Best Practices for Prevention

1. **Adhere Strictly to Cases**: Ensure you align your casing with the registry above. For instance, `CommandName` must not be spelled as `commandname` or `Command`.
