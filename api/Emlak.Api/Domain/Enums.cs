namespace Emlak.Api.Domain;

public enum ListingType { Satilik, Kiralik }

public enum Currency { TRY, USD, EUR }

public enum HeatingType { Yok, Soba, Dogalgaz, Kombi, Merkezi, YerdenIsitma, Klima }

public enum FurnishingStatus { Esyali, Esyasiz }

public enum UsageStatus { Bos, KiraciOturuyor, MalSahibiOturuyor }

public enum DeedStatus { KatMulkiyeti, KatIrtifaki, HisseliTapu, MustakilTapu }

public enum KitchenType { Acik, Kapali, Amerikan }

public enum TagGroup { Ic, Dis, Konum }

public enum UserRole { Admin, Field }
