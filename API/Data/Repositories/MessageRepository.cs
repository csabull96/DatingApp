using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class MessageRepository : IMessageRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        public MessageRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;

        }
        public void AddMessage(Message message)
        {
            _context.Messages.Add(message);
        }

        public void DeleteMessage(Message message)
        {
            _context.Messages.Remove(message);
        }

        public async Task<Message> GetMessage(int id)
        {
            return await _context.Messages
                .Include(message => message.Sender)
                .Include(message => message.Recipient)
                .SingleOrDefaultAsync(message => message.Id == id);
        }

        public async Task<PagedList<MessageDto>> GetMessagesForUser(MessageParams messageParams)
        {
            var query = _context.Messages
                .OrderByDescending(message => message.MessageSent)
                .AsQueryable();

            query = messageParams.Container switch
            {
                "Inbox" => query.Where(message => message.Recipient.UserName == messageParams.Username && !message.RecipientDeleted),
                "Outbox" => query.Where(message => message.Sender.UserName == messageParams.Username && !message.SenderDeleted),
                _ => query.Where(message => message.Recipient.UserName == messageParams.Username && !message.RecipientDeleted && message.DateRead == null)
            };

            var messages = query.ProjectTo<MessageDto>(_mapper.ConfigurationProvider);

            return await PagedList<MessageDto>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);
        }

        public async Task<IEnumerable<MessageDto>> GetMessageThread(string currentUsername, string recipientUsername)
        {
            var messages = await _context.Messages
                .Include(message => message.Sender).ThenInclude(user => user.Photos)
                .Include(message => message.Recipient).ThenInclude(user => user.Photos)
                .Where(message => message.Recipient.UserName == currentUsername 
                    && message.Sender.UserName == recipientUsername && !message.RecipientDeleted
                    || message.Recipient.UserName == recipientUsername 
                    && message.Sender.UserName == currentUsername && !message.SenderDeleted
                )
                .OrderBy(message => message.MessageSent)
                .ToListAsync();

            var unreadMessages = messages.Where(message => message.DateRead == null && message.Recipient.UserName == currentUsername)
                .ToList();

            
            if (unreadMessages.Any())
            {
                unreadMessages.ForEach(unreadMessage => unreadMessage.DateRead = DateTime.Now);
                await _context.SaveChangesAsync();
            }

            return _mapper.Map<IEnumerable<MessageDto>>(messages);
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}