// FantasyWorld.Server/Services/Entertainment/PhotoService.cs
using Microsoft.EntityFrameworkCore;
using FantasyWorld.Server.Data;
using FantasyWorld.Server.Data.Entities;
using FantasyWorld.Shared.DTOs;

namespace FantasyWorld.Server.Services.Entertainment;

public interface IPhotoService
{
    Task<(bool Ok, string Msg, long PhotoId)> TakePhotoAsync(long charId, TakePhotoRequest req, string lang);
    Task<List<PhotoDto>>    GetMyPhotosAsync(long charId, int page);
    Task<List<PhotoDto>>    GetPublicPhotosAsync(int mapId, int page);
    Task<(bool Ok, string Msg)> LikeAsync(long charId, long photoId, string lang);
    Task<(bool Ok, string Msg)> DeleteAsync(long charId, long photoId, string lang);
    Task<List<PhotoFrame>>  GetFramesAsync();
    Task<(bool Ok, string Msg, long AlbumId)> CreateAlbumAsync(long charId, string name, string lang);
    Task<(bool Ok, string Msg)> AddToAlbumAsync(long charId, long albumId, long photoId, string lang);
    Task<List<PhotoAlbum>>  GetAlbumsAsync(long charId);
}

public class PhotoService(
    GameDbContext        db,
    ILocalizationService loc) : IPhotoService
{
    public async Task<(bool, string, long)> TakePhotoAsync(
        long charId, TakePhotoRequest req, string lang)
    {
        var photo = new Photo
        {
            TakerId      = charId,
            PhotoUrl     = req.PhotoUrl,
            PhotoType    = req.PhotoType,
            FrameId      = req.FrameId,
            MapId        = req.MapId,
            PosX         = req.PosX,
            PosY         = req.PosY,
            StickersJson = req.StickersJson,
            TaggedChars  = req.TaggedCharIds.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(req.TaggedCharIds) : null,
            Caption      = req.Caption,
        };
        db.Photos.Add(photo);
        await db.SaveChangesAsync();
        return (true, "OK", photo.Id);
    }

    public async Task<List<PhotoDto>> GetMyPhotosAsync(long charId, int page) =>
        await db.Photos
            .Where(p => p.TakerId == charId)
            .Include(p => p.Taker)
            .OrderByDescending(p => p.TakenAt)
            .Skip((page - 1) * 20).Take(20)
            .Select(p => new PhotoDto(
                p.Id, p.Taker.Name, p.PhotoUrl,
                p.PhotoType, p.Caption, p.Likes,
                new DateTimeOffset(p.TakenAt).ToUnixTimeMilliseconds()))
            .ToListAsync();

    public async Task<List<PhotoDto>> GetPublicPhotosAsync(int mapId, int page) =>
        await db.Photos
            .Where(p => p.MapId == mapId && p.IsPublic)
            .Include(p => p.Taker)
            .OrderByDescending(p => p.Likes).ThenByDescending(p => p.TakenAt)
            .Skip((page - 1) * 20).Take(20)
            .Select(p => new PhotoDto(
                p.Id, p.Taker.Name, p.PhotoUrl,
                p.PhotoType, p.Caption, p.Likes,
                new DateTimeOffset(p.TakenAt).ToUnixTimeMilliseconds()))
            .ToListAsync();

    public async Task<(bool, string)> LikeAsync(long charId, long photoId, string lang)
    {
        var photo = await db.Photos.FindAsync(photoId);
        if (photo is null) return (false, loc.Get("error.not_found", lang));

        // Simple like (no unlike for now)
        photo.Likes++;
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<(bool, string)> DeleteAsync(long charId, long photoId, string lang)
    {
        var photo = await db.Photos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.TakerId == charId);
        if (photo is null) return (false, loc.Get("error.not_found", lang));

        db.Photos.Remove(photo);
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<PhotoFrame>> GetFramesAsync() =>
        await db.PhotoFrames.ToListAsync();

    public async Task<(bool, string, long)> CreateAlbumAsync(
        long charId, string name, string lang)
    {
        var count = await db.PhotoAlbums.CountAsync(a => a.OwnerId == charId);
        if (count >= 20)
            return (false, loc.Get("error.bad_request", lang), 0);

        var album = new PhotoAlbum { OwnerId = charId, Name = name.Trim() };
        db.PhotoAlbums.Add(album);
        await db.SaveChangesAsync();
        return (true, "OK", album.Id);
    }

    public async Task<(bool, string)> AddToAlbumAsync(
        long charId, long albumId, long photoId, string lang)
    {
        var album = await db.PhotoAlbums
            .FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == charId);
        if (album is null) return (false, loc.Get("error.not_found", lang));

        var exists = await db.AlbumPhotos
            .AnyAsync(ap => ap.AlbumId == albumId && ap.PhotoId == photoId);
        if (exists) return (false, loc.Get("error.bad_request", lang));

        db.AlbumPhotos.Add(new AlbumPhoto { AlbumId = albumId, PhotoId = photoId });
        await db.SaveChangesAsync();
        return (true, "OK");
    }

    public async Task<List<PhotoAlbum>> GetAlbumsAsync(long charId) =>
        await db.PhotoAlbums
            .Where(a => a.OwnerId == charId)
            .Include(a => a.Photos)
            .ToListAsync();
}
