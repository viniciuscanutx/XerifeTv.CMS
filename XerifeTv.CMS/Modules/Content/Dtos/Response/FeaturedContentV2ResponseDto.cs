namespace XerifeTv.CMS.Modules.Content.Dtos.Response;

// Item do carrossel de destaques: o conteúdo (filme ou série) + "movie"/"series"
public record FeaturedContentV2ResponseDto(object Content, string Type);
