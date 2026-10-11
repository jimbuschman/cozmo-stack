# Q14-104/131: callback input,76-byte entries and settings array fields

Checkpoint736; guard reread before each step. Primary evidence: final-reader-fields-native.txt, final-reader-inputs-native.txt and final-reader-products-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Destination and input boundary |
| --- | --- |
| 9BB5B0 / 9BB5B8 | Actual9BB4A4 secondargumentr1→SL;SB=SP4 is supplied toA67E84 before laterSL38 publication, GOT-derived rawpointer→SL34, packed4 bit0 before notification9A96E4/9AF778. Conditional callback input matches the local contract ofAW735 but receiver identity is not inferred from that match. UNKNOWN secondarg/type/indirect producer beyond known inputs remains. |
| 9BFEB8 / 9BFEBC |9BFE58..70 obtains4C-byte slot fromSL array0/count4/cap8, incrementscount before null-slot branch; initializes independent record34/38 to binary32unity3F800000 at9BFE80, alongside3C/48. Exact defaults/ordering, not per-sample DSP. RootSL input remains conditional; no all-alias claim. |
| 9BFF98 / 9BFFA0 | Capacity replacement branch separately allocates4C*(cap+1) bytes at9BFF44, retains result atSP24 thenR4; separate newrecord34/38 binary32unity before oldrecord transfer. Positive independent buffer destination, not parent-node ID. Root array input remains conditional. |
| 9CDD08 / 9CDD1C | Actual9CDC4C destructor publishes SAME103AF98 table as9CDBF0/SL433F settings product (AW681); count34 clears before releasebase30, thenbase30/cap38 clear. Known settings receiver positive; alternate receiver inputs not exhausted. |
| 9CDF2C / 9CDFC4 | Actual9CDEC4 settings insertion reads30/34, searches8-byte key/value pairs, and assigns incomingr0→R8;34 count advances before computedslotnull test, key/value publication follows. Capacity replacement publishesbase30 thencap38 after release. ExistingSL433F/AW681 supplies settings object; alternate insertion receiver/key/value inputs remain conditional. Decision-bearing property order retained, no blanket storage exclusion. |

out of scope: backing allocation/copy/free arithmetic; no descent. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
