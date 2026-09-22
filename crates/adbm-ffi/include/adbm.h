/*
 * ADB Manager core — C ABI.
 *
 * All strings are UTF-8, NUL-terminated JSON. Schemas are the serde types in
 * crates/adbm-core/src/api.rs (Command, Response, Event, Config).
 *
 * Threading: adbm_core_call and adbm_core_next_event may be called from any
 * thread, concurrently. Stop the thread that polls events before calling
 * adbm_core_free.
 */
#ifndef ADBM_H
#define ADBM_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct AdbmCore AdbmCore;

/* Create a core. config_json may be NULL for defaults. NULL on failure;
 * call adbm_last_error for the reason. */
AdbmCore *adbm_core_new(const char *config_json);

/* Run a Command. Returns a Response (never NULL). Free with adbm_string_free. */
char *adbm_core_call(AdbmCore *core, const char *command_json);

/* Next Event, waiting up to timeout_ms. NULL on timeout. Free with adbm_string_free. */
char *adbm_core_next_event(AdbmCore *core, uint32_t timeout_ms);

/* Stop all work and free the core. NULL is ignored. */
void adbm_core_free(AdbmCore *core);

/* Free a string returned by this library. NULL is ignored. */
void adbm_string_free(char *s);

/* Last error from adbm_core_new on this thread, or NULL. Free with adbm_string_free. */
char *adbm_last_error(void);

/* Library version. Static, do not free. */
const char *adbm_version(void);

#ifdef __cplusplus
}
#endif

#endif /* ADBM_H */
