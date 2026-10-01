let token, session, conversationId, version, pollGeneration=0;
const $ = id => document.getElementById(id);
const notice = text => { $('notice').textContent = text; };
const providerLabel = id => id?.startsWith('ollama:') ? 'Asistente · IA local real' : 'Asistente · proveedor simulado';
async function csrf(){const response=await fetch('/v1/session/csrf');if(!response.ok)throw new Error('No pudimos preparar la sesión.');token=(await response.json()).token;$('csrf-form').value=token;}
async function request(path,method='GET',data,headers={}){const response=await fetch(path,{method,headers:{...(data?{'Content-Type':'application/json'}:{}),...(method!=='GET'?{'X-CSRF-TOKEN':token}:{}),...headers},body:data?JSON.stringify(data):undefined});const raw=await response.text();const body=raw?JSON.parse(raw):null;if(!response.ok){const messages={RESOURCE_NOT_FOUND:'Esa conversación no está disponible para este usuario.',SESSION_REQUIRED:'Tu sesión terminó. Vuelve a entrar.',SENSITIVE_PAYMENT_DATA:'Usa datos ficticios. No incluyas tarjetas ni códigos de pago.',VERSION_CONFLICT:'La conversación cambió. Ábrela de nuevo.',RESERVATION_SOURCE_UNAVAILABLE:'La fuente de reservas no respondió. Puedes volver a consultar.',VERIFIED_CUSTOMER_REQUIRED:'Esta consulta requiere una cuenta de cliente.',OPERATIONAL_STORE_UNAVAILABLE:'No se pudo guardar el mensaje. Puedes intentarlo de nuevo.',OFFER_EXPIRED:'La propuesta venció. Solicita otra.',OFFER_CONSUMED:'Esta propuesta ya fue resuelta. Actualiza el estado.',SOURCE_VERSION_CONFLICT:'La reserva cambió. Solicita otra propuesta.',HUMAN_OWNS_CONVERSATION:'La conversación fue transferida. El bot está en pausa.',OPERATION_PENDING:'Hay una operación pendiente. Revisa su resultado.',TRANSACTIONS_DISABLED:'Las cancelaciones están pausadas.',CANCELLATION_NOT_ELIGIBLE:'Esta reserva no cumple la regla de cancelación.'};throw new Error(messages[body?.code]||'No pudimos completar este paso.');}return body;}
function storedKey(){return 'ccai-demo-conversation-'+session.displayName;}
async function showConversation(){
    $('reservations-panel').classList.add('hidden');
    const snapshot=await request('/v1/conversations/'+conversationId);
    const assistant=await request('/v1/conversations/'+conversationId+'/assistant');
    version=Math.max(snapshot.version,assistant.version); $('messages').replaceChildren();
    for(const message of snapshot.messages){
        const bubble=document.createElement('div');bubble.className='bubble';bubble.textContent=message.text;
        const note=document.createElement('div');note.className='message-note';note.textContent='Guardado';bubble.append(note);$('messages').append(bubble);
        const turn=assistant.turns.find(item=>item.turnId===message.turnId);
        if(turn){
            const answer=document.createElement('div');answer.className='bubble assistant-bubble';answer.textContent=turn.answer.text;
            const source=document.createElement('div');source.className='message-note';source.textContent=providerLabel(turn.answer.providerId);answer.append(source);
            for(const citation of turn.answer.citations){const button=document.createElement('button');button.className='citation';button.textContent=citation.documentId+' v'+citation.version+' · '+citation.sectionId;button.addEventListener('click',()=>openCitation(citation));answer.append(button);}
            if(!session.isEmployee){const feedback=document.createElement('button');feedback.className='feedback';feedback.textContent='Reportar respuesta';feedback.addEventListener('click',async()=>{try{await request('/v1/conversations/'+conversationId+'/feedback','POST',{turnId:turn.turnId,reason:'incorrect'});feedback.disabled=true;notice('Reporte guardado para revisión.');}catch(error){notice(error.message);}});answer.append(feedback);}
            $('messages').append(answer);
        }
    }
    const ownership=assistant.ownership==='HumanOwned'?'Humano asignado en el mock · bot en pausa':assistant.ownership==='HandoffPending'?'Transferencia solicitada · esperando aceptación':'Conversación abierta';
    $('conversation-state').textContent=ownership+' · '+(snapshot.language==='es'?'Español':'English');
    $('language').value=snapshot.language; $('messages').scrollTop=$('messages').scrollHeight;
    $('handoff-button').classList.toggle('hidden',session.isEmployee||assistant.ownership!=='AI');
    if(!session.isEmployee)await showActions();
    return assistant;
}
async function main(){
    const profile=await request('/v1/demo/profile');const live=profile.mode==='local-llm';
    $('provider-badge').textContent=live?(profile.retrieval?.startsWith('semantic')?'IA REAL · BÚSQUEDA SEMÁNTICA':'IA REAL · MODELO LOCAL'):'ASISTENTE SIMULADO · LOCAL';
    $('provider-hint').textContent='Prueba: «¿Cuál es la política de cancelación?», «Mis reservas» o «Cancelar RES-001». '+(live?'La IA local interpreta tu petición.':'El proveedor de intención está simulado.')+' La cancelación ficticia requiere el botón Confirmar; el chat nunca la ejecuta directamente.';
    $('provider-footer').textContent='ContactCenterAI · C# / .NET · Datos sintéticos · '+(live?'IA real local para interpretar intenciones':'Proveedor de IA simulado')+' · Contact center simulado';
    const response=await fetch('/v1/session');
    if(response.ok){session=await response.json();if(session.authenticated){
        $('welcome').classList.add('hidden');$('workspace').classList.remove('hidden');$('account').textContent=session.displayName;$('logout').classList.remove('hidden');await csrf();
        if(session.isOperationsAdmin){$('workspace').classList.add('hidden');$('operations-panel').classList.remove('hidden');await showOperations();return;}
        if(session.isEmployee){
            $('workspace-title').textContent='Contexto del agente asignado';$('new').classList.add('hidden');$('reservations-button').classList.add('hidden');$('compose').classList.add('hidden');$('handoff-button').classList.add('hidden');$('agent-panel').classList.remove('hidden');
            await loadAssignments();return;
        }
        conversationId=localStorage.getItem(storedKey());if(conversationId){try{const assistant=await showConversation();if(assistant.pendingTurns)pollAssistant(++pollGeneration);}catch{localStorage.removeItem(storedKey());conversationId=null;}}return;
    }}await csrf();
}
$('new').addEventListener('click',async()=>{try{const created=await request('/v1/conversations','POST',{language:$('language').value},{'Idempotency-Key':crypto.randomUUID()});pollGeneration++;conversationId=created.conversationId;version=created.version;localStorage.setItem(storedKey(),conversationId);await showConversation();notice('Conversación creada. Puedes escribir un mensaje.');}catch(error){notice(error.message);}});
$('compose').addEventListener('submit',async event=>{event.preventDefault();if(!conversationId){notice('Pulsa Nueva conversación para empezar.');return;}const text=$('text').value.trim();if(!text)return;const button=event.currentTarget.querySelector('button');button.disabled=true;try{const receipt=await request('/v1/conversations/'+conversationId+'/messages','POST',{clientMessageId:crypto.randomUUID(),text},{'If-Match':'"'+version+'"'});$('text').value='';await showConversation();$('receipt').textContent='Mensaje guardado. Esperando la respuesta del asistente.';notice('Mensaje guardado en tu historial.');pollAssistant(++pollGeneration);}catch(error){notice(error.message);}finally{button.disabled=false;}});
$('logout').addEventListener('click',async()=>{try{await request('/v1/session/logout','POST');location.reload();}catch(error){notice(error.message);}});
main().catch(error=>notice(error.message));

$('reservations-button').addEventListener('click',async()=>{
    if(!conversationId){notice('Crea una conversación para consultar tus reservas.');return;}
    const button=$('reservations-button');button.disabled=true;
    $('reservation-list').replaceChildren();$('reservations-panel').classList.remove('hidden');
    notice('Consultando tus reservas…');
    try{
        const items=await request('/v1/conversations/'+conversationId+'/reservations');
        for(const item of items){
            const card=document.createElement('article');card.className='reservation-card';
            const title=document.createElement('h4');title.textContent=item.reservationId+' · '+item.propertyName;
            const date=document.createElement('p');date.className='small';date.textContent='Llegada: '+new Intl.DateTimeFormat('es',{dateStyle:'medium',timeStyle:'short',timeZone:'UTC'}).format(new Date(item.checkInUtc))+' UTC';
            const status=document.createElement('p');status.className='eligibility '+(item.cancellation.eligible?'eligible':'ineligible');
            status.textContent=item.cancellation.eligible?'Cumple la regla de cancelación gratuita.':item.status==='Cancelled'?'La reserva está cancelada en la fuente ficticia.':'Quedan menos de 72 horas. No se ofrece cancelación gratuita.';
            card.append(title,date,status);$('reservation-list').append(card);
            if(item.cancellation.eligible){const preview=document.createElement('button');preview.className='secondary';preview.textContent='Ver propuesta de cancelación';preview.addEventListener('click',()=>previewCancellation(item.reservationId,preview));card.append(preview);}
        }
        notice(items.length?'Reservas consultadas. No se ha realizado ninguna cancelación.':'No tienes reservas en esta demostración.');
    }catch(error){notice(error.message);}finally{button.disabled=false;}
});

async function previewCancellation(reservationId,button){button.disabled=true;try{const offer=await request('/v1/conversations/'+conversationId+'/cancellation-offers','POST',{reservationId},{'If-Match':'"'+version+'"'});version=offer.version;await showActions();notice('Revisa la propuesta. Solo el botón Confirmar ejecuta la cancelación ficticia.');}catch(error){notice(error.message);}finally{button.disabled=false;}}
async function decideOffer(offer,decision,button){button.disabled=true;const storage='ccai-confirm-'+offer.offerId+'-'+decision;let key=localStorage.getItem(storage);if(!key){key=crypto.randomUUID();localStorage.setItem(storage,key);}try{const receipt=await request('/v1/conversations/'+conversationId+'/confirmations','POST',{offerId:offer.offerId,decision},{'If-Match':'"'+offer.version+'"','Idempotency-Key':key});version=receipt.version;await showActions();notice(receipt.operationId?'Solicitud guardada. Esperando el resultado de la fuente.':'Propuesta rechazada. La reserva sigue igual.');if(receipt.operationId)pollOperation(receipt.operationId,++pollGeneration);}catch(error){notice(error.message);await showActions();}finally{button.disabled=false;}}
function operationText(operation){const english=$('language').value==='en';if(operation.status==='Completed')return english?'Cancellation confirmed by the source. Reference: '+operation.sourceReference:'Cancelación confirmada por la fuente. Referencia: '+operation.sourceReference;if(operation.status==='Unknown')return english?'Outcome uncertain. Reconciliation in progress. Do not repeat.':'Resultado incierto. Comprobando con la fuente. No repitas la acción.';if(operation.status==='Conflict')return english?'The reservation changed. Please request a new offer.':'La reserva cambió. Solicita una nueva propuesta.';if(operation.status==='Rejected')return english?'Request rejected. No confirmed cancellation.':'Solicitud rechazada. No se ha confirmado una cancelación.';return english?'Request saved. Waiting for the source.':'Solicitud guardada. Esperando confirmación de la fuente.';}
async function showActions(){if(!conversationId)return;const actions=await request('/v1/conversations/'+conversationId+'/actions');$('action-list').replaceChildren();$('actions-panel').classList.toggle('hidden',!actions.offers.some(offer=>offer.status==='Active')&&!actions.operations.length);for(const offer of actions.offers.filter(item=>item.status==='Active')){const card=document.createElement('article');card.className='offer-card';const heading=document.createElement('h4');heading.textContent='Propuesta · '+offer.reservationId;const description=document.createElement('p');description.textContent='Cancelar la reserva sin penalidad: 0 '+offer.currency+'. No hay devolución ni cobro en esta demo.';const expiry=document.createElement('p');expiry.className='small';expiry.textContent='Válida hasta '+new Date(offer.expiresAt).toLocaleTimeString()+' · '+offer.policyVersion;const confirm=document.createElement('button');confirm.className='primary';confirm.textContent='Confirmar cancelación ficticia';confirm.addEventListener('click',()=>decideOffer(offer,'confirm',confirm));const reject=document.createElement('button');reject.className='secondary';reject.textContent='Conservar reserva';reject.addEventListener('click',()=>decideOffer(offer,'reject',reject));const controls=document.createElement('div');controls.className='offer-controls';controls.append(confirm,reject);card.append(heading,description,expiry,controls);$('action-list').append(card);}for(const operation of actions.operations){const card=document.createElement('article');card.className='operation-card '+(operation.status==='Completed'?'completed':'');const title=document.createElement('strong');title.textContent=operation.reservationId+' · '+operation.status;const detail=document.createElement('p');detail.textContent=operationText(operation);const id=document.createElement('p');id.className='small';id.textContent='Operación: '+operation.operationId+(operation.requiresHumanReview?' · Revisión humana requerida':'');card.append(title,detail,id);$('action-list').append(card);}}
async function pollOperation(id,generation){for(let attempt=0;attempt<12&&generation===pollGeneration;attempt++){await new Promise(resolve=>setTimeout(resolve,2000));if(generation!==pollGeneration)return;try{const operation=await request('/v1/operations/'+id);await showActions();if(['Completed','Rejected','Conflict'].includes(operation.status)){notice(operationText(operation));return;}}catch(error){notice(error.message);return;}}if(generation===pollGeneration)notice('Puedes actualizar el estado cuando quieras. La solicitud sigue guardada.');}
$('refresh-actions').addEventListener('click',()=>showActions().catch(error=>notice(error.message)));

async function pollAssistant(generation){
    for(let attempt=0;attempt<12&&generation===pollGeneration;attempt++){
        await new Promise(resolve=>setTimeout(resolve,1500));if(generation!==pollGeneration)return;
        try{const assistant=await showConversation();if(assistant.pendingTurns===0&&assistant.ownership!=='HandoffPending'){$('receipt').textContent='Respuesta guardada · '+(assistant.ownership==='HumanOwned'?'humano asignado en el mock':'asistente');return;}}
        catch(error){notice(error.message);return;}
    }
    if(generation===pollGeneration)notice('El mensaje sigue guardado. Puedes actualizar la conversación.');
}
async function openCitation(citation){
    try{const evidence=await request('/v1/conversations/'+conversationId+'/citations/'+encodeURIComponent(citation.documentId)+'/'+citation.version+'/'+encodeURIComponent(citation.sectionId));$('citation-panel').classList.remove('hidden');$('citation-title').textContent=evidence.title+' · v'+evidence.version;$('citation-content').textContent=evidence.content;}
    catch(error){notice(error.message);}
}
async function loadAssignments(){
    const assignments=await request('/v1/agent/conversations');$('assigned-list').replaceChildren();
    for(const assignment of assignments){const button=document.createElement('button');button.className='secondary';button.textContent='Abrir caso '+assignment.conversationId.slice(0,8);button.addEventListener('click',()=>showAgentContext(assignment.conversationId));$('assigned-list').append(button);}
    if(assignments.length)await showAgentContext(assignments[assignments.length-1].conversationId);else notice('No hay casos asignados a esta cuenta.');
}
async function showAgentContext(id){
    try{const context=await request('/v1/agent/conversations/'+id+'/context');conversationId=id;await showConversation();$('agent-summary').textContent=context.summary;$('agent-case-id').textContent='Caso: '+id;}
    catch(error){notice(error.message);}
}
$('refresh-conversation').addEventListener('click',()=>{if(conversationId)showConversation().catch(error=>notice(error.message));});
$('handoff-button').addEventListener('click',async()=>{if(!conversationId){notice('Crea una conversación primero.');return;}try{await request('/v1/conversations/'+conversationId+'/handoff','POST');await showConversation();pollAssistant(++pollGeneration);notice('Atención humana solicitada al contact center simulado.');}catch(error){notice(error.message);}});
$('internal-guide').addEventListener('click',()=>openCitation({documentId:'KB-AGENT-'+$('language').value.toUpperCase(),version:1,sectionId:'overview'}));

async function showOperations(){
    const result=await request('/v1/operations/status');const op=result.operational;
    $('operations-counts').replaceChildren();
    for(const [label,count] of [['Respuestas pendientes',op.pendingTurns],['Documentos aprobados',op.knowledgeVariants],['Documentos listos para búsqueda',op.indexedVariants],['Cancelaciones confirmadas',op.commands.completed],['Resultados inciertos',op.commands.unknown],['Revisión humana',op.commands.humanReview]]){
        const card=document.createElement('article');card.className='reservation-card';const title=document.createElement('strong');title.textContent=label;const value=document.createElement('h2');value.textContent=count;card.append(title,value);$('operations-counts').append(card);
    }
    $('operations-alerts').replaceChildren();for(const text of op.alerts){const alert=document.createElement('p');alert.textContent=text;$('operations-alerts').append(alert);}
    $('operations-traces').replaceChildren();const labels={'api.request':'Consulta a la demo','assistant.turn':'Respuesta del asistente','knowledge.search':'Búsqueda de información','knowledge.rerank':'Selección de fuente','source.read':'Consulta de reservas','source.cancel':'Solicitud de cancelación','source.receipt':'Comprobación del resultado'};
    for(const trace of result.telemetry.recent){const row=document.createElement('p');row.className='small';row.textContent=(labels[trace.operation]||'Actividad')+' · '+trace.durationMs+' ms'+(trace.httpStatus?' · HTTP '+trace.httpStatus:'')+' · seguimiento '+trace.traceId.slice(0,12);$('operations-traces').append(row);}
}
$('refresh-operations').addEventListener('click',()=>showOperations().catch(error=>notice(error.message)));
