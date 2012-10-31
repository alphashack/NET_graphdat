Graphdat SqlTrace Reader Service
====

Windows service to monitor the sql traces generated when running the graphdat sql trace script.

----

Reads the trace entries from the (second last) trace file - the one that is not currently in use by sql server.
Canonicalises the queries, removing variable values etc.
Sends the data to the graphdat agent.
